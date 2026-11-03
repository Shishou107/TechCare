namespace API_FixComputerCenter.Security;

public static class Permissions
{
    // Customer
    public const string CustomerView = "Customer.View";
    public const string CustomerCreate = "Customer.Create";
    public const string CustomerUpdate = "Customer.Update";

    // Repair Ticket
    public const string RepairTicketView = "Repair.Ticket.View";
    public const string RepairTicketCreate = "Repair.Ticket.Create";
    public const string RepairTicketUpdate = "Repair.Ticket.Update";
    public const string RepairTicketAssign = "Repair.Ticket.Assign";

    // Diagnosis & Quotation
    public const string RepairDiagnosisCreate = "Repair.Diagnosis.Create";
    public const string RepairQuotationCreate = "Repair.Quotation.Create";
    public const string RepairQuotationApprove = "Repair.Quotation.Approve";

    // Inventory
    public const string InventoryStockView = "Inventory.Stock.View";
    public const string InventoryStockIssue = "Inventory.Stock.Issue";

    // Billing
    public const string BillingPaymentCreate = "Billing.Payment.Create";
    public const string BillingInvoiceView = "Billing.Invoice.View";

    // Warranty
    public const string WarrantyClaimCreate = "Warranty.Claim.Create";
}

