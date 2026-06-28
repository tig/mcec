using System;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Helpers;

public class LoggerTests
{
    [Fact]
    public void Instance_IsSingleton()
    {
        var instance1 = Logger.Instance;
        var instance2 = Logger.Instance;

        Assert.NotNull(instance1);
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void LogFile_DefaultsToLogFileInConfigPath()
    {
        var logger = Logger.Instance;
        Assert.Contains("MCEControl.log", logger.LogFile);
    }

    [Fact]
    public void LogFile_CanBeSet()
    {
        var logger = Logger.Instance;
        string originalPath = logger.LogFile;

        try
        {
            string testPath = @"C:\temp\test.log";
            logger.LogFile = testPath;
            Assert.Equal(testPath, logger.LogFile);
        }
        finally
        {
            logger.LogFile = originalPath;
        }
    }

    [Fact]
    public void Log4_IsNotNull()
    {
        var logger = Logger.Instance;
        Assert.NotNull(logger.Log4);
    }
}
