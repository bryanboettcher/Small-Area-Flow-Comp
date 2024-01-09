public class ErrorLogger
{
    // ErrorLogger variables
    private readonly string _logFilePath;

    // Constructor
    public ErrorLogger(string logFileName)
    {
        var scriptDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _logFilePath = Path.Combine(scriptDirectory, logFileName);

        // Try to create or open the log file for writing
        try
        {
            using (StreamWriter writer = File.AppendText(_logFilePath))
            {
                // Write a message to the log file
                writer.WriteLine($"Log file accessed at: {DateTime.Now}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating or writing to the log file: {ex.Message}");
        }
    }

    public void AddToLog(string msg)
    {
        try
        {
            // Add text to the log file at logFilePath
            File.AppendAllText(_logFilePath, $"\n{DateTime.Now}: {msg}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error appending to the log file: {ex.Message}");
        }
    }
}