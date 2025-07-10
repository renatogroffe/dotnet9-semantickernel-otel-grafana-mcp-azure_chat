using ConsoleAppChatMCP.Inputs;
using ConsoleAppChatMCP.Tracing;
using Grafana.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var standardForegroundColor = ConsoleColor.White;
Console.ForegroundColor = standardForegroundColor;
Console.WriteLine("***** Testes com Semantic Kernel + Plugins (Kernel Functions) + MCP Azure *****");
Console.WriteLine();

var aiSolution = InputHelper.GetAISolution();

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService(OpenTelemetryExtensions.ServiceName);

AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

var traceProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(OpenTelemetryExtensions.ServiceName)
    .AddSource("Microsoft.SemanticKernel*")
    .AddHttpClientInstrumentation()
    .UseGrafana()
    .Build();

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var kernelBuilder = Kernel.CreateBuilder();
PromptExecutionSettings settings;

if (aiSolution == InputHelper.OLLAMA)
{
    kernelBuilder.AddOllamaChatCompletion(
        modelId: configuration["Ollama:Model"]!,
        endpoint: new Uri(configuration["Ollama:Endpoint"]!),
        serviceId: "chat");
    settings = new OllamaPromptExecutionSettings
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
    };
}
else if (aiSolution == InputHelper.AZURE_OPEN_AI)
{
    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: configuration["AzureOpenAI:DeploymentName"]!,
        endpoint: configuration["AzureOpenAI:Endpoint"]!,
        apiKey: configuration["AzureOpenAI:ApiKey"]!,
        serviceId: "chat");
    settings = new OpenAIPromptExecutionSettings
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
    };
}
else
    throw new Exception($"Solucao de AI invalida: {aiSolution}");

var mcpName = configuration["MCP:Name"]!;
await using var mcpClient = await McpClientFactory.CreateAsync(new SseClientTransport(new()
{
    Name = mcpName,
    Endpoint = new Uri(configuration["MCP:Endpoint"]!)
}));

Console.Write("Ferramentas do MCP: ");
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"***** {mcpName} *****");
Console.WriteLine();
var tools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
Console.WriteLine($"Quantidade de ferramentas disponiveis = {tools.Count}");
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Green;
foreach (var tool in tools)
{
    Console.WriteLine($"* {tool.Name}: {tool.Description}");
}
Console.ForegroundColor = standardForegroundColor;
Console.WriteLine();

Kernel kernel = kernelBuilder.Build();
kernel.Plugins.AddFromFunctions(mcpName,
    tools.Select(aiFunction => aiFunction.AsKernelFunction()));

var aiChatService = kernel.GetRequiredService<IChatCompletionService>();
var chatHistory = new ChatHistory();
while (true)
{
    Console.WriteLine("Sua pergunta:");
    Console.ForegroundColor = ConsoleColor.Cyan;
    var userPrompt = Console.ReadLine();
    Console.ForegroundColor = standardForegroundColor;

    using var activity1 = OpenTelemetryExtensions.ActivitySource
        .StartActivity("PerguntaChatIAMCP")!;

    chatHistory.Add(new ChatMessageContent(AuthorRole.User, userPrompt));

    Console.WriteLine();
    Console.WriteLine("Resposta da IA:");
    Console.WriteLine();

    ChatMessageContent chatResult = await aiChatService
        .GetChatMessageContentAsync(chatHistory, settings, kernel);
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(chatResult.Content);
    Console.ForegroundColor = standardForegroundColor;
    chatHistory.Add(new ChatMessageContent(AuthorRole.Assistant, chatResult.Content));

    Console.WriteLine();
    Console.WriteLine();

    activity1.Stop();
}

#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
