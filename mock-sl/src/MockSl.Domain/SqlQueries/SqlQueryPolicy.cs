using MockSl.Domain.Errors;

namespace MockSl.Domain.SqlQueries;

/// <summary>Who may do what with saved SQL, and which tables it may read.</summary>
public static class SqlQueryPolicy
{
    /// <summary>
    /// The tables of the guide's allowlist (p.145-147) that exist in the mock.
    /// OHEM, OACT, OUSR and OSQL are not on that list, so SQL cannot read them.
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedTables = new HashSet<string>
    {
        "OCRD",
        "OINV",
        "INV1",
        "INV6",
        "OPCH",
        "PCH1",
        "ORCT",
        "RCT2",
        "OVPM",
        "VPM2",
        "OITM",
        "OITW",
        "OACP",
    };

    /// <summary>
    /// A normal user may read a saved query, but not run or change it (guide p.157-159).
    /// Real SAP can grant more through General Authorizations, which the mock does not have.
    /// </summary>
    public static void EnsurePermitted(bool isSuperUser, bool isPlainRead)
    {
        if (!isSuperUser && !isPlainRead)
        {
            throw SapErrors.NotPermitted();
        }
    }
}
