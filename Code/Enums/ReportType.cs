﻿using System;

namespace Observation;

/// <summary>
/// When added to an <see cref="Anomaly.AnomalyType"/>, it makes the anomaly reportable by room instead of by cursor.
/// </summary>
[AttributeUsage( AttributeTargets.Field )]
public class RoomReportAttribute : Attribute;

/// <summary>
/// Which reporting type to use.
/// </summary>
public enum ReportType
{
	Cursor,
	Room
}
