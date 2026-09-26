using MediatR;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Rates.Commands;

public sealed class DeleteRateCommandHandler : IRequestHandler<DeleteRateCommand, bool>
{
    private readonly IRepository<Rate> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRateCommandHandler(IRepository<Rate> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteRateCommand request, CancellationToken cancellationToken)
    {
        var rate = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (rate == null)
            return false;

        await _repository.DeleteAsync(rate, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}