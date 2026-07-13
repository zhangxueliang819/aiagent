using AgentPlatform.AgentEngine.Skills;
using AgentPlatform.AgentEngine.Skills.Implementations;
using AgentPlatform.ModelProviders.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;
using System.Text.Json;

Console.WriteLine("Hello, World!");

var key = "sk-57c64876144b4c1c81ee78a45cc70e9d";
var openAiClient = new OpenAIClient(
	new ApiKeyCredential(key),
	new OpenAIClientOptions()
	{
		Endpoint = new Uri("https://api.deepseek.com/v1"),		
	}
	
);

var textAnalyzerSkill= new TextAnalyzerSkill();
var chatClient = openAiClient.GetChatClient("deepseek-chat");
var testf= new FunctionToolAIFunction(
				name: textAnalyzerSkill.Name,
				description: textAnalyzerSkill.Description,
				jsonSchema: JsonSerializer.Deserialize<JsonElement>(textAnalyzerSkill.InputSchemaJson) ,
				executeAsync: (args, ct2) => textAnalyzerSkill.ExecuteAsync(args, ct2));

var fun1 = AIFunctionFactory.Create(GetWeather);
//var agent = chatClient.AsAIAgent(
//	instructions: "你是一个有用的助手，可以分析文本。",
//	tools: [fun1, testf]
//);
//var resp1 = await agent.RunAsync("帮我分析文本：中国人");


// 2. 构建 ChatClientAgentOptions
var options = new Microsoft.Agents.AI.ChatClientAgentOptions
{
	Name = "文本分析",
	Description = "你是一个有用的助手，可以分析文本",
};
options.ChatOptions = new ChatOptions();
options.ChatOptions.Tools = new List<AITool>();
options.ChatOptions.Tools.Add(fun1);
options.ChatOptions.Tools.Add(testf);

var _chatClient=new OpenAIProvider(
			"https://api.deepseek.com/v1",
			key,
			"deepseek-v4-flash",
			new HttpClient(), new Logger<OpenAIProvider>(new LoggerFactory())
		);

var  resp= await _chatClient.AsAIAgent(name: "文本分析", description: "你是一个有用的助手，可以分析文本", tools: [fun1, testf]).RunAsync("帮我分析文本：中国人");
Console.WriteLine(resp);

[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
	=> $"The weather in {location} is cloudy with a high of 15°C.";