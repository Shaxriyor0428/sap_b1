using GroceryStore;
using Sap.Application.Errors;

const int Accepted = 0;
const int ValuesDiffer = 1;
const int CouldNotRun = 2;

var settings = GrocerySettings.FromEnvironment();
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

using var serviceLayer = new ServiceLayerConnection(settings);

try
{
    await serviceLayer.LoginAsync(settings.UserName, settings.Password, cancellation.Token);

    var ledger = await new ScenarioRunner(serviceLayer, settings, Console.Out).RunAsync(cancellation.Token);
    var isCorrect = await new Verifier(serviceLayer, Console.Out).VerifyAsync(ledger, cancellation.Token);

    await serviceLayer.LogoutAsync(cancellation.Token);
    return isCorrect ? Accepted : ValuesDiffer;
}
catch (ServiceLayerErrorException exception)
{
    Console.Error.WriteLine($"Service Layer error {exception.Code} (HTTP {exception.StatusCode}): {exception.Message}");
    Console.Error.WriteLine("If the codes already exist, use another GROCERY_PREFIX or, on the mock, run: docker compose down -v");
    return CouldNotRun;
}
catch (ServiceLayerUnavailableException exception)
{
    Console.Error.WriteLine($"{exception.Message} {exception.InnerException?.Message}");
    return CouldNotRun;
}
