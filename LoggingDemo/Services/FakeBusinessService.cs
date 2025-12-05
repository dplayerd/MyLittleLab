namespace LoggingDemo.Services
{
    public class FakeBusinessService
    {
    private readonly ILogger<FakeBusinessService> _logger;
        public FakeBusinessService(ILogger<FakeBusinessService> logger)
        {
            this._logger = logger;
        }

        public void PerformOperation()
        {
            this._logger.LogTrace("Starting business operation.");

            try
            {
                Thread.Sleep(500);


                //this._logger.LogDebug("This is an Debug log. of FakeBusinessService");
                //this._logger.LogTrace("This is an Trace log. of FakeBusinessService");
                //this._logger.LogInformation("This is an info log. of FakeBusinessService");
                //this._logger.LogWarning("This is a warning log. of FakeBusinessService");
                //this._logger.LogError("This is an error log. of FakeBusinessService");

                this._logger.LogInformation("Business operation Success.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "");

                throw;
            }

            this._logger.LogTrace("Ending business operation.");
        }
    }
}
