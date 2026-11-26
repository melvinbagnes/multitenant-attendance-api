namespace AttendanceApi.Services;

public sealed record HoursBreakdown(decimal TotalHours, decimal RegularHours, decimal OvertimeHours);

public static class OvertimeCalculator
{
	/// <summary>
	/// Splits the worked time of one day into regular and overtime hours.
	/// Total is rounded to 2 decimals first, so Regular + Overtime always equals Total.
	/// </summary>
	public static HoursBreakdown Calculate(IEnumerable<TimeSpan> sessions, decimal regularThresholdHours)
	{
		ArgumentNullException.ThrowIfNull(sessions);

		if (regularThresholdHours < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(regularThresholdHours), "Threshold cannot be negative.");
		}

		var total = 0m;

		foreach (var session in sessions)
		{
			if (session < TimeSpan.Zero)
			{
				throw new ArgumentOutOfRangeException(nameof(sessions), "A session duration cannot be negative.");
			}

			total += (decimal)session.TotalHours;
		}

		total = Math.Round(total, 2, MidpointRounding.AwayFromZero);
		var regular = Math.Min(total, regularThresholdHours);

		return new HoursBreakdown(total, regular, total - regular);
	}
}
