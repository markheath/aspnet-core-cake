#addin nuget:?package=Cake.Kudu.Client&version=0.3.0

// Target - The task you want to start. Runs the Default task if not specified.
var target = Argument("Target", "Default");  
var configuration = Argument("Configuration", "Release");
//var mySetting = EnvironmentVariable("my_setting") ?? "default value";

Information($"Running target {target} in configuration {configuration}");

var distDirectory = Directory("./dist");

// Deletes the contents of the Artifacts folder if it contains anything from a previous build.
Task("Clean")  
    .Does(() =>
    {
        CleanDirectory(distDirectory);
    });

// Run dotnet restore to restore all package references.
Task("Restore")  
    .Does(() =>
    {
        DotNetCoreRestore();
    });

// Build using the build configuration specified as an argument.
 Task("Build")
    .Does(() =>
    {
        DotNetCoreBuild(".",
            new DotNetCoreBuildSettings()
            {
                Configuration = configuration,
                ArgumentCustomization = args => args.Append("--no-restore"),
            });
    });

// Look under a 'Tests' folder and run dotnet test against all of those projects.
// Then drop the XML test results file in the Artifacts folder at the root.
Task("Test")  
    .Does(() =>
    {
        var projects = GetFiles("./test/**/*.csproj");
        foreach(var project in projects)
        {
            Information("Testing project " + project);
            DotNetCoreTest(
                project.ToString(),
                new DotNetCoreTestSettings()
                {
                    Configuration = configuration,
                    NoBuild = true,
                    ArgumentCustomization = args => args.Append("--no-restore"),
                });
        }
    });

// Publish the app to the /dist folder
Task("PublishWeb")  
    .Does(() =>
    {
        DotNetCorePublish(
            "./coreapp.csproj",
            new DotNetCorePublishSettings()
            {
                Configuration = configuration,
                OutputDirectory = distDirectory,
                ArgumentCustomization = args => args.Append("--no-restore"),
            });
    });

Task("DeployToAzure")
    .Description("Deploy to Azure ")
    .Does(() =>
    {
        // https://hackernoon.com/run-from-zip-with-cake-kudu-client-5c063cd72b37
        string baseUri  = EnvironmentVariable("KUDU_CLIENT_BASEURI"),
               userName = EnvironmentVariable("KUDU_CLIENT_USERNAME"),
               password = EnvironmentVariable("KUDU_CLIENT_PASSWORD");
        Information($"Kudu deploy to {baseUri} {userName} {password}");
        IKuduClient kuduClient = KuduClient(
            baseUri,
            userName,
            password);
        FilePath deployFilePath = kuduClient.ZipRunFromDirectory(distDirectory);
        Information("Deployed to {0}", deployFilePath);
    });

// A meta-task that runs all the steps to Build and Test the app
Task("BuildAndTest")  
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("Test");

// The default task to run if none is explicitly specified. In this case, we want
// to run everything starting from Clean, all the way up to Publish.
Task("Default")  
    .IsDependentOn("BuildAndTest")
    .IsDependentOn("PublishWeb")
    .IsDependentOn("DeployToAzure");

// Executes the task specified in the target argument.
RunTarget(target);