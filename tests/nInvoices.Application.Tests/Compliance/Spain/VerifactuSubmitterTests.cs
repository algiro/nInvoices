using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Moq;
using nInvoices.Application.Compliance;
using nInvoices.Application.Compliance.Spain;
using nInvoices.Application.Compliance.Spain.Verifactu;
using nInvoices.Application.Services.Email;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance.Spain;

internal sealed class FakeAeatClient : IAeatVerifactuClient
{
    public List<(AeatTarget Target, XElement Batch, string Thumbprint)> Calls { get; } = [];
    public Func<XElement, AeatResponse> Answer { get; set; } = _ => throw new AeatException("no answer set");

    public Task<AeatResponse> SendAsync(AeatTarget target, X509Certificate2 certificate, XElement batch, CancellationToken cancellationToken = default)
    {
        Calls.Add((target, batch, certificate.Thumbprint));
        return Task.FromResult(Answer(batch));
    }
}

[TestFixture]
public sealed class VerifactuSubmitterTests
{
    private InMemoryRepository<VerifactuSubmission> _submissions = null!;
    private FakeVerifactuRecords _records = null!;
    private InMemoryRepository<ComplianceSettings> _settings = null!;
    private FakeAeatClient _client = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private FixedTimeProvider _time = null!;
    private VerifactuOptions _options = null!;
    private X509Certificate2 _certificate = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private sealed class PlainProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => ciphertext;
    }

    [OneTimeSetUp]
    public void OneTimeSetUp() => _certificate = FacturaeTestData.NewCertificate();

    [OneTimeTearDown]
    public void OneTimeTearDown() => _certificate.Dispose();

    [SetUp]
    public void SetUp()
    {
        _time = new FixedTimeProvider(new DateTime(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc));
        _submissions = new InMemoryRepository<VerifactuSubmission>();
        _records = new FakeVerifactuRecords();
        _settings = new InMemoryRepository<ComplianceSettings>();
        _client = new FakeAeatClient();
        _unitOfWork = new Mock<IUnitOfWork>();
        _options = new VerifactuOptions { Environment = "Test", ProducerName = "Producer SL", ProducerTaxId = "B12345674" };

        WithCertificate();
    }

    private void WithCertificate(bool seal = false)
    {
        _settings.Items.Clear();
        var settings = new ComplianceSettings("ES");
        var values = new Dictionary<string, string> { [SpainComplianceModule.VerifactuKey] = "true" };
        if (seal) values[SpainComplianceModule.SealCertificateKey] = "true";
        settings.Update(true, "Ana Pérez García", "12345678Z", null, values);
        settings.SetCertificate(Convert.ToBase64String(_certificate.Export(X509ContentType.Pfx, "pw")), "pw",
            _certificate.Subject, _certificate.Thumbprint, _certificate.NotAfter.ToUniversalTime());
        _settings.Items.Add(settings);
    }

    private VerifactuSubmitter Submitter() => new(
        _submissions, _records, _settings, new SigningCertificateLoader(new PlainProtector(), _time),
        _client, _unitOfWork.Object, Options.Create(_options), _time);

    /// <summary>Adds records 1..count, each with its pending submission.</summary>
    private void Queue(int count, int from = 1)
    {
        for (var i = from; i < from + count; i++)
        {
            var number = $"26-10-{i:000}";
            var stamp = "2026-10-03T11:30:00+02:00";
            var hash = VerifactuHash.Issued("12345678Z", number, "03-10-2026", "F1", "21.00", "121.00", "", stamp);
            var xml = VerifactuXml.Issued(
                new VerifactuInvoiceData("12345678Z", "Ana", number, new DateOnly(2026, 10, 3), "F1", "Servicios",
                    new VerifactuRecipient("Cliente", "A58818501", null, null, null), [new VerifactuBreakdown("S1", 21m, 100m, 21m)], 21m, 121m),
                new VerifactuSystem("Producer SL", "B12345674", "nInvoices", "NI", "1.0", "1", false), null, stamp, hash);
            var record = new VerifactuRecord(i, VerifactuRecordKind.Issued, i, "12345678Z", number, "03-10-2026", "F1", "21.00", "121.00", "", stamp, hash, xml.ToString());
            record.Id = i;
            _records.Items.Add(record);
            _submissions.Items.Add(new VerifactuSubmission(record.Id, record.Sequence, _time.Now) { Id = i });
        }
    }

    private static AeatResponse Accepted(XElement batch, int wait = 60)
    {
        var numbers = batch.Descendants(VerifactuXml.Sf + "NumSerieFactura").Select(e => e.Value).Distinct().ToList();
        return new AeatResponse("A-1234", wait, "Correcto", numbers.Select(n => new AeatLineResult(n, "Alta", "Correcto", null, null)).ToList());
    }

    // --- Nothing to do ----------------------------------------------------------------------------

    [Test]
    public async Task NothingPending_NothingIsSent()
    {
        var run = await Submitter().SubmitPendingAsync(Token);

        run.ShouldBe(new SubmissionRun(0, 0, 0, 0, null));
        _client.Calls.ShouldBeEmpty();
    }

    // --- Sending ----------------------------------------------------------------------------------

    [Test]
    public async Task Pending_AreSentInChainOrder_AndMarkedAccepted()
    {
        Queue(3);
        _client.Answer = batch => Accepted(batch);

        var run = await Submitter().SubmitPendingAsync(Token);

        run.ShouldBe(new SubmissionRun(3, 3, 0, 0, null));
        var call = _client.Calls.ShouldHaveSingleItem();
        call.Batch.Descendants(VerifactuXml.Sf + "NumSerieFactura").Select(e => e.Value).Distinct()
            .ShouldBe(["26-10-001", "26-10-002", "26-10-003"]);
        call.Batch.Descendants(VerifactuXml.Sf + "ObligadoEmision").Single().Element(VerifactuXml.Sf + "NIF")!.Value.ShouldBe("12345678Z");
        call.Thumbprint.ShouldBe(_certificate.Thumbprint);

        _submissions.Items.ShouldAllBe(s => s.Status == VerifactuSubmissionStatus.Accepted && s.Csv == "A-1234");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task TheBatchSent_IsValidAgainstTheAeatSchema()
    {
        Queue(2);
        _client.Answer = batch => Accepted(batch);

        await Submitter().SubmitPendingAsync(Token);

        // The records are stored as XML and put back together for the submission: the result must still be valid
        var batch = _client.Calls.Single().Batch;
        batch.Name.ShouldBe(VerifactuXml.Lr + "RegFactuSistemaFacturacion");
        batch.Elements(VerifactuXml.Lr + "RegistroFactura").Count().ShouldBe(2);
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(false, true, true)]
    public async Task Target_FollowsTheEnvironmentAndTheKindOfCertificate(bool production, bool seal, bool expectedSeal)
    {
        Queue(1);
        _options = new VerifactuOptions { Environment = production ? "Production" : "Test", ProducerName = "P", ProducerTaxId = "B12345674" };
        WithCertificate(seal);
        _client.Answer = batch => Accepted(batch);

        await Submitter().SubmitPendingAsync(Token);

        _client.Calls.Single().Target.ShouldBe(new AeatTarget(production, expectedSeal));
    }

    [Test]
    public async Task OnlyOneBatchIsSent_AtMostHundredRecords()
    {
        Queue(120);
        _client.Answer = batch => Accepted(batch);

        var run = await Submitter().SubmitPendingAsync(Token);

        run.Sent.ShouldBe(VerifactuSubmitter.BatchSize);
        _submissions.Items.Count(s => s.Status == VerifactuSubmissionStatus.Accepted).ShouldBe(100);
        _submissions.Items.Count(s => s.Status == VerifactuSubmissionStatus.Pending).ShouldBe(20);
    }

    [Test]
    public async Task WhatIsLeft_WaitsForThePauseTheTaxAgencyAskedFor()
    {
        Queue(120);
        _client.Answer = batch => Accepted(batch, wait: 90);
        var submitter = Submitter();

        await submitter.SubmitPendingAsync(Token);
        var left = _submissions.Items.Where(s => s.Status == VerifactuSubmissionStatus.Pending).ToList();
        left.ShouldAllBe(s => s.NextAttemptAt == _time.Now.AddSeconds(90));

        // Too soon: nothing is sent
        _time.Now = _time.Now.AddSeconds(30);
        (await submitter.SubmitPendingAsync(Token)).Sent.ShouldBe(0);
        _client.Calls.Count.ShouldBe(1);

        // After the pause, the rest goes
        _time.Now = _time.Now.AddSeconds(61);
        (await submitter.SubmitPendingAsync(Token)).Sent.ShouldBe(20);
        _client.Calls.Count.ShouldBe(2);
    }

    // --- What the Tax Agency answers -------------------------------------------------------------------

    [Test]
    public async Task RecordsAcceptedWithErrors_AreKeptWithTheirRemark()
    {
        Queue(1);
        _client.Answer = _ => new AeatResponse("A-9", 60, "ParcialmenteCorrecto",
            [new AeatLineResult("26-10-001", "Alta", "AceptadoConErrores", "2000", "La huella no coincide")]);

        var run = await Submitter().SubmitPendingAsync(Token);

        run.AcceptedWithErrors.ShouldBe(1);
        var submission = _submissions.Items.Single();
        submission.Status.ShouldBe(VerifactuSubmissionStatus.AcceptedWithErrors);
        submission.ErrorCode.ShouldBe("2000");
        submission.Message.ShouldBe("La huella no coincide");
    }

    [Test]
    public async Task ARejectedRecord_StopsTheOnesAfterIt()
    {
        Queue(3);
        _client.Answer = _ => new AeatResponse("A-9", 60, "ParcialmenteCorrecto",
        [
            new AeatLineResult("26-10-001", "Alta", "Correcto", null, null),
            new AeatLineResult("26-10-002", "Alta", "Incorrecto", "1100", "Valor o formato incorrecto"),
            new AeatLineResult("26-10-003", "Alta", "Correcto", null, null)
        ]);
        var submitter = Submitter();

        var run = await submitter.SubmitPendingAsync(Token);

        run.Rejected.ShouldBe(1);
        _submissions.Items.Select(s => s.Status).ShouldBe(
            [VerifactuSubmissionStatus.Accepted, VerifactuSubmissionStatus.Rejected, VerifactuSubmissionStatus.Accepted]);

        // A later record is queued behind the rejected one: it is not sent
        Queue(1, from: 4);
        var again = await submitter.SubmitPendingAsync(Token);
        again.Sent.ShouldBe(0);
        again.Problem.ShouldNotBeNull().ShouldContain("Record 2 was rejected");
        _client.Calls.Count.ShouldBe(1);
    }

    [Test]
    public async Task ARecordTheTaxAgencyAlreadyHas_CountsAsAccepted()
    {
        Queue(1);
        _client.Answer = _ => new AeatResponse("A-9", 60, "Incorrecto",
            [new AeatLineResult("26-10-001", "Alta", "Incorrecto", "3000", "Registro de facturación duplicado")]);

        var run = await Submitter().SubmitPendingAsync(Token);

        run.Accepted.ShouldBe(1);
        _submissions.Items.Single().Status.ShouldBe(VerifactuSubmissionStatus.Accepted);
    }

    [Test]
    public async Task ARecordWithoutAnAnswer_StaysPending()
    {
        Queue(2);
        _client.Answer = _ => new AeatResponse("A-9", 60, "ParcialmenteCorrecto",
            [new AeatLineResult("26-10-001", "Alta", "Correcto", null, null)]);

        await Submitter().SubmitPendingAsync(Token);

        _submissions.Items[0].Status.ShouldBe(VerifactuSubmissionStatus.Accepted);
        _submissions.Items[1].Status.ShouldBe(VerifactuSubmissionStatus.Pending);
        _submissions.Items[1].Message.ShouldNotBeNull().ShouldContain("no answer");
    }

    [Test]
    public async Task ASubmissionRefusedAsAWhole_IsRetriedAfterThePause()
    {
        Queue(1);
        _client.Answer = _ => new AeatResponse(null, 60, "Incorrecto", []);

        var run = await Submitter().SubmitPendingAsync(Token);

        run.Problem.ShouldNotBeNull().ShouldContain("did not accept");
        var submission = _submissions.Items.Single();
        submission.Status.ShouldBe(VerifactuSubmissionStatus.Pending);
        submission.NextAttemptAt.ShouldBe(_time.Now.AddSeconds(60));
    }

    // --- When sending fails -------------------------------------------------------------------------

    [Test]
    public async Task ATransportFailure_IsRetriedWithABackoff()
    {
        Queue(1);
        _client.Answer = _ => throw new AeatException("The Tax Agency could not be reached");
        var submitter = Submitter();

        var run = await submitter.SubmitPendingAsync(Token);

        run.Problem.ShouldNotBeNull().ShouldContain("could not be reached");
        var submission = _submissions.Items.Single();
        submission.Status.ShouldBe(VerifactuSubmissionStatus.Pending);
        submission.Attempts.ShouldBe(1);
        submission.NextAttemptAt.ShouldBe(_time.Now.AddMinutes(1));

        _time.Now = _time.Now.AddMinutes(1);
        await submitter.SubmitPendingAsync(Token);
        submission.Attempts.ShouldBe(2);
        submission.NextAttemptAt.ShouldBe(_time.Now.AddMinutes(2));
    }

    [Test]
    public async Task WithoutACertificate_NothingIsSent_AndTheReasonIsGiven()
    {
        Queue(1);
        _settings.Items[0].ClearCertificate();

        var run = await Submitter().SubmitPendingAsync(Token);

        run.Sent.ShouldBe(0);
        run.Problem.ShouldNotBeNull().ShouldContain("certificate");
        _client.Calls.ShouldBeEmpty();
        _submissions.Items.Single().Attempts.ShouldBe(0);
    }

    [Test]
    public async Task ServerNotSetUp_NothingIsSent()
    {
        Queue(1);
        _options = new VerifactuOptions();

        var run = await Submitter().SubmitPendingAsync(Token);

        run.Problem.ShouldNotBeNull().ShouldContain("not set up");
        _client.Calls.ShouldBeEmpty();
    }

    // --- The summary ---------------------------------------------------------------------------------

    [Test]
    public async Task Summary_CountsByStatus_AndPointsAtTheFirstProblem()
    {
        Queue(4);
        var submissions = _submissions.Items;
        submissions[0].Answered(VerifactuSubmissionStatus.Accepted, "A", null, null, _time.Now);
        submissions[1].Answered(VerifactuSubmissionStatus.AcceptedWithErrors, "A", "2000", "La huella no coincide", _time.Now);
        submissions[2].Answered(VerifactuSubmissionStatus.Rejected, "A", "1100", "Formato", _time.Now);

        var summary = await Submitter().GetSummaryAsync(Token);

        summary.ShouldBe(new SubmissionSummary(1, 1, 1, 1, "Record 2: La huella no coincide"));
    }

    [Test]
    public async Task Summary_ReportsAFailingSubmission()
    {
        Queue(1);
        _submissions.Items[0].Failed("could not be reached", _time.Now, TimeSpan.FromMinutes(1));

        (await Submitter().GetSummaryAsync(Token)).FirstProblem.ShouldBe("Record 1 could not be sent: could not be reached");
    }
}
