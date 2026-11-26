using AttendanceApi.Services;

namespace AttendanceApi.Tests;

public class OvertimeCalculatorTests
{
	[Fact]
	public void NoSessions_ReturnsZeros()
	{
		var result = OvertimeCalculator.Calculate([], 8m);

		Assert.Equal(0m, result.TotalHours);
		Assert.Equal(0m, result.RegularHours);
		Assert.Equal(0m, result.OvertimeHours);
	}

	[Fact]
	public void BelowThreshold_IsAllRegular()
	{
		var result = OvertimeCalculator.Calculate([TimeSpan.FromHours(7.5)], 8m);

		Assert.Equal(7.5m, result.TotalHours);
		Assert.Equal(7.5m, result.RegularHours);
		Assert.Equal(0m, result.OvertimeHours);
	}

	[Fact]
	public void AboveThreshold_SplitsRegularAndOvertime()
	{
		var result = OvertimeCalculator.Calculate([TimeSpan.FromHours(10)], 8m);

		Assert.Equal(10m, result.TotalHours);
		Assert.Equal(8m, result.RegularHours);
		Assert.Equal(2m, result.OvertimeHours);
	}

	[Fact]
	public void MultipleSessions_AreSummedBeforeSplitting()
	{
		var result = OvertimeCalculator.Calculate([TimeSpan.FromHours(4), TimeSpan.FromHours(5)], 8m);

		Assert.Equal(9m, result.TotalHours);
		Assert.Equal(8m, result.RegularHours);
		Assert.Equal(1m, result.OvertimeHours);
	}

	[Fact]
	public void Total_IsRoundedToTwoDecimals_AndStillAddsUp()
	{
		var result = OvertimeCalculator.Calculate([TimeSpan.FromMinutes(500)], 8m);

		Assert.Equal(8.33m, result.TotalHours);
		Assert.Equal(8m, result.RegularHours);
		Assert.Equal(0.33m, result.OvertimeHours);
		Assert.Equal(result.TotalHours, result.RegularHours + result.OvertimeHours);
	}

	[Fact]
	public void NegativeSession_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OvertimeCalculator.Calculate([TimeSpan.FromHours(-1)], 8m));
	}

	[Fact]
	public void NegativeThreshold_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OvertimeCalculator.Calculate([TimeSpan.FromHours(1)], -1m));
	}
}
