// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

public class CommandResultTests {
    [Fact]
    public void Ok_SetsSuccessAndCommand() {
        CommandResult result = CommandResult.Ok("capture");

        Assert.True(result.Success);
        Assert.Equal("capture", result.Command);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Ok_WithData_StoresProvidedData() {
        JsonObject data = new() { ["foo"] = "bar" };

        CommandResult result = CommandResult.Ok("query", data);

        Assert.True(result.Success);
        Assert.Same(data, result.Data);
    }

    [Fact]
    public void Fail_SetsSuccessFalseCommandAndError() {
        CommandResult result = CommandResult.Fail("find", "boom");

        Assert.False(result.Success);
        Assert.Equal("find", result.Command);
        Assert.Equal("boom", result.Error);
        Assert.Null(result.Data);
    }

    [Fact]
    public void ToJson_ContainsSuccessProperty() {
        string json = CommandResult.Ok("capture").ToJson();

        Assert.Contains("success", json);
    }

    [Fact]
    public void ToJsonObject_RoundTripsDataIntoDataObject() {
        JsonObject data = new() { ["width"] = 640, ["encoding"] = "png" };

        JsonObject obj = CommandResult.Ok("capture", data).ToJsonObject();

        Assert.True(obj["success"]!.GetValue<bool>());
        JsonObject dataObj = Assert.IsType<JsonObject>(obj["data"]);
        Assert.Equal(640, dataObj["width"]!.GetValue<int>());
        Assert.Equal("png", dataObj["encoding"]!.GetValue<string>());
    }

    [Fact]
    public void ToJsonObject_Failure_IncludesErrorAndOmitsData() {
        JsonObject obj = CommandResult.Fail("invoke", "nope").ToJsonObject();

        Assert.False(obj["success"]!.GetValue<bool>());
        Assert.Equal("nope", obj["error"]!.GetValue<string>());
        Assert.Null(obj["data"]);
    }
}
