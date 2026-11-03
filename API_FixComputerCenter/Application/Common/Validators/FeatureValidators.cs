using API_FixComputerCenter.Application.Features.Auth;
using API_FixComputerCenter.Application.Features.Inventory;
using API_FixComputerCenter.Application.Features.Quotations;
using API_FixComputerCenter.Application.Features.Repairs;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace API_FixComputerCenter.Application.Common.Validators;

public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("Tên đăng nhập không được để trống.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Mật khẩu không được để trống.");
    }
}

public class CreateQuotationValidator : AbstractValidator<CreateQuotationRequest>
{
    public CreateQuotationValidator()
    {
        RuleFor(x => x.RepairTicketId).GreaterThan(0).WithMessage("Mã phiếu sửa chữa không hợp lệ.");
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0).WithMessage("Tiền giảm giá không được âm.");
        RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0).WithMessage("Tiền thuế không được âm.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Báo giá phải có ít nhất 1 hạng mục dịch vụ/linh kiện.");
        RuleFor(x => x).Must(q => q.Items == null || q.DiscountAmount <= q.Items.Sum(i => i.Quantity * i.UnitPrice))
            .WithMessage("Tiền giảm giá không được vượt quá tổng giá trị các hạng mục (Subtotal).");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description).NotEmpty().WithMessage("Mô tả hạng mục không được để trống.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.");
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Đơn giá không được âm.");
        });
    }
}

public class UpdateRepairStatusValidator : AbstractValidator<UpdateRepairStatusRequest>
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "RECEIVED", "DIAGNOSING", "WAITING_APPROVAL", "WAITING_PARTS",
        "REPAIRING", "QUALITY_CHECK", "READY_FOR_PICKUP", "COMPLETED",
        "RETURNED", "CANCELLED", "DELIVERED"
    };

    public UpdateRepairStatusValidator()
    {
        RuleFor(x => x.NewStatus)
            .NotEmpty().WithMessage("Trạng thái mới không được để trống.")
            .Must(s => ValidStatuses.Contains(s))
            .WithMessage("Trạng thái chuyển đổi không hợp lệ theo quy định hệ thống.");
    }
}

public class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator != null)
        {
            var argument = context.Arguments.OfType<T>().FirstOrDefault();
            if (argument != null)
            {
                var validationResult = await validator.ValidateAsync(argument);
                if (!validationResult.IsValid)
                {
                    return Results.ValidationProblem(validationResult.ToDictionary());
                }
            }
        }

        return await next(context);
    }
}

public static class ValidationExtensions
{
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<ValidationFilter<T>>();
    }
}
