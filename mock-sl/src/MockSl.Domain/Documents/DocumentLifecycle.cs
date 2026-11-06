using MockSl.Domain.Errors;

namespace MockSl.Domain.Documents;

/// <summary>Which status changes a document allows. Values are as stored in the tables.</summary>
public static class DocumentLifecycle
{
    public const string Open = "O";

    public const string Closed = "C";

    public const string Yes = "Y";

    public const string No = "N";

    public static void EnsureCanCancel(string canceled, string status, decimal paidToDate)
    {
        if (canceled != No)
        {
            throw SapErrors.DocumentAlreadyCancelled();
        }

        // Cancelling first would leave a payment pointing at a document that no longer counts.
        if (paidToDate > 0)
        {
            throw SapErrors.DocumentHasPayments();
        }

        if (status == Closed)
        {
            throw SapErrors.DocumentAlreadyClosed();
        }
    }

    public static void EnsureCanClose(string canceled, string status)
    {
        if (canceled != No)
        {
            throw SapErrors.DocumentAlreadyCancelled();
        }

        if (status == Closed)
        {
            throw SapErrors.DocumentAlreadyClosed();
        }
    }
}
