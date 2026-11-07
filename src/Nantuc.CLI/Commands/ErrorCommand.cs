namespace Nantuc.CLI.Commands;

public class ErrorCommand(string message, TextWriter wr)
{
    public int Execute()
    {
        wr.WriteLine(message);
        return -2;
    }
}
