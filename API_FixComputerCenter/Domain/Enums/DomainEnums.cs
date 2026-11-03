namespace API_FixComputerCenter.Domain.Enums;

public enum AppointmentStatus
{
    BOOKED,
    CONFIRMED,
    CHECKED_IN,
    CANCELLED,
    NO_SHOW
}

public enum RepairStatus
{
    RECEIVED,
    DIAGNOSING,
    QUOTATION_PENDING,
    QUOTATION_APPROVED,
    QUOTATION_REJECTED,
    REPAIRING,
    QA_TESTING,
    READY_FOR_PICKUP,
    DELIVERED,
    CANCELLED
}

public enum QuotationStatus
{
    DRAFT,
    SENT,
    APPROVED,
    REJECTED,
    EXPIRED
}

public enum PaymentStatus
{
    PENDING,
    PARTIALLY_PAID,
    PAID,
    REFUNDED
}

