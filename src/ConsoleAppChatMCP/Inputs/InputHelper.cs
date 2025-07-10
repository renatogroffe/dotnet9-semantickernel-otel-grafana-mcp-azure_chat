using Sharprompt;

namespace ConsoleAppChatMCP.Inputs;

public class InputHelper
{
    public const string AZURE_OPEN_AI = "AzureOpenAI";
    public const string OLLAMA = "Ollama";

    public static string GetAISolution()
    {
        var answer = Prompt.Select<string>(options =>
        {
            options.Message = "Selecione a quantidade de registros a ser gerada";
            options.Items = [AZURE_OPEN_AI, OLLAMA];
        });
        Console.WriteLine();
        return answer;
    }
}