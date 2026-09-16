namespace RaceIQ.Infrastructure.ZwiftPower;

// The pasted ZwiftPower text could not be read as a results document. Distinct from a
// generic failure so the web layer can show a "paste the JSON again" message instead of
// an error page.
public class ZwiftPowerImportException : Exception
{
    public ZwiftPowerImportException(string message, Exception? inner = null) : base(message, inner) { }
}
