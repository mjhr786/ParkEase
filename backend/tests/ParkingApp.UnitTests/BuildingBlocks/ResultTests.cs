using System;
using System.Threading.Tasks;
using FluentAssertions;
using ParkingApp.BuildingBlocks.Common;
using Xunit;

namespace ParkingApp.UnitTests.BuildingBlocks;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        var result = Result.Success();
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        var result = Result.Failure("Test Error", "TEST_CODE");
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Test Error");
        result.ErrorCode.Should().Be("TEST_CODE");
    }

    [Fact]
    public void GenericSuccess_ShouldCreateSuccessfulResultWithValue()
    {
        var result = Result.Success(42);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void GenericFailure_ShouldCreateFailedResult()
    {
        var result = Result.Failure<int>("Error", "ERR");
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Be(default);
        result.Error.Should().Be("Error");
        result.ErrorCode.Should().Be("ERR");
    }

    [Fact]
    public void ImplicitOperator_ShouldConvertValueToSuccessResult()
    {
        Result<string> result = "test";
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("test");
    }
}

public class ResultExtensionsTests
{
    [Fact]
    public void Map_ShouldTransformValue_WhenSuccess()
    {
        var result = Result.Success(5);
        var mapped = result.Map(x => x * 2);
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public void Map_ShouldPropagateFailure_WhenFailure()
    {
        var result = Result.Failure<int>("Err", "CODE");
        var mapped = result.Map(x => x.ToString());
        mapped.IsSuccess.Should().BeFalse();
        mapped.Error.Should().Be("Err");
        mapped.ErrorCode.Should().Be("CODE");
    }

    [Fact]
    public async Task MapAsync_ShouldTransformValue_WhenSuccess()
    {
        var result = Result.Success(5);
        var mapped = await result.MapAsync(async x => { await Task.Delay(1); return x * 2; });
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public async Task MapAsync_ShouldPropagateFailure_WhenFailure()
    {
        var result = Result.Failure<int>("Err", "CODE");
        var mapped = await result.MapAsync(async x => { await Task.Delay(1); return x.ToString(); });
        mapped.IsSuccess.Should().BeFalse();
        mapped.Error.Should().Be("Err");
        mapped.ErrorCode.Should().Be("CODE");
    }

    [Fact]
    public void Bind_ShouldExecuteBinder_WhenSuccess()
    {
        var result = Result.Success(5);
        var bound = result.Bind(x => Result.Success(x.ToString()));
        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("5");
    }

    [Fact]
    public void Bind_ShouldPropagateFailure_WhenFailure()
    {
        var result = Result.Failure<int>("Err", "CODE");
        var bound = result.Bind(x => Result.Success(x.ToString()));
        bound.IsSuccess.Should().BeFalse();
        bound.Error.Should().Be("Err");
        bound.ErrorCode.Should().Be("CODE");
    }

    [Fact]
    public void Match_NonGeneric_ShouldExecuteOnSuccess_WhenSuccess()
    {
        var result = Result.Success();
        var val = result.Match(() => 1, err => 0);
        val.Should().Be(1);
    }

    [Fact]
    public void Match_NonGeneric_ShouldExecuteOnFailure_WhenFailure()
    {
        var result = Result.Failure("Err");
        var val = result.Match(() => 1, err => 0);
        val.Should().Be(0);
    }

    [Fact]
    public void Match_Generic_ShouldExecuteOnSuccess_WhenSuccess()
    {
        var result = Result.Success("Test");
        var val = result.Match(x => x.Length, err => 0);
        val.Should().Be(4);
    }

    [Fact]
    public void Match_Generic_ShouldExecuteOnFailure_WhenFailure()
    {
        var result = Result.Failure<string>("Err");
        var val = result.Match(x => x.Length, err => 0);
        val.Should().Be(0);
    }
}
