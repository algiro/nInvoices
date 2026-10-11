using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.BusinessExpenses;

namespace nInvoices.Api.Controllers;

/// <summary>
/// The costs of the user's business, deducted in the tax prediction: one-off expenses and those paid
/// every month (such as the social security fee).
/// </summary>
[ApiController]
[Route("api/business-expenses")]
[Authorize]
public sealed class BusinessExpensesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BusinessExpensesController> _logger;

    public BusinessExpensesController(IMediator mediator, ILogger<BusinessExpensesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>The expenses dated in a year, oldest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BusinessExpenseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<BusinessExpenseDto>>> GetByYear([FromQuery] int year, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetBusinessExpensesQuery(year), cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(BusinessExpenseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BusinessExpenseDto>> Create([FromBody] SaveBusinessExpenseDto dto, CancellationToken cancellationToken)
    {
        var expense = await _mediator.Send(new CreateBusinessExpenseCommand(dto), cancellationToken);
        _logger.LogInformation("Created business expense {ExpenseId}", expense.Id);
        return Ok(expense);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(BusinessExpenseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusinessExpenseDto>> Update(long id, [FromBody] SaveBusinessExpenseDto dto, CancellationToken cancellationToken)
    {
        var expense = await _mediator.Send(new UpdateBusinessExpenseCommand(id, dto), cancellationToken);
        _logger.LogInformation("Updated business expense {ExpenseId}", id);
        return Ok(expense);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        if (!await _mediator.Send(new DeleteBusinessExpenseCommand(id), cancellationToken))
            return NotFound();

        _logger.LogInformation("Deleted business expense {ExpenseId}", id);
        return NoContent();
    }

    /// <summary>Every recurring expense, by the month it starts.</summary>
    [HttpGet("recurring")]
    [ProducesResponseType(typeof(IReadOnlyList<RecurringExpenseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecurringExpenseDto>>> GetRecurring(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetRecurringExpensesQuery(), cancellationToken));
    }

    [HttpPost("recurring")]
    [ProducesResponseType(typeof(RecurringExpenseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecurringExpenseDto>> CreateRecurring([FromBody] SaveRecurringExpenseDto dto, CancellationToken cancellationToken)
    {
        var expense = await _mediator.Send(new CreateRecurringExpenseCommand(dto), cancellationToken);
        _logger.LogInformation("Created recurring expense {ExpenseId}", expense.Id);
        return Ok(expense);
    }

    [HttpPut("recurring/{id}")]
    [ProducesResponseType(typeof(RecurringExpenseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecurringExpenseDto>> UpdateRecurring(long id, [FromBody] SaveRecurringExpenseDto dto, CancellationToken cancellationToken)
    {
        var expense = await _mediator.Send(new UpdateRecurringExpenseCommand(id, dto), cancellationToken);
        _logger.LogInformation("Updated recurring expense {ExpenseId}", id);
        return Ok(expense);
    }

    [HttpDelete("recurring/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRecurring(long id, CancellationToken cancellationToken)
    {
        if (!await _mediator.Send(new DeleteRecurringExpenseCommand(id), cancellationToken))
            return NotFound();

        _logger.LogInformation("Deleted recurring expense {ExpenseId}", id);
        return NoContent();
    }
}
