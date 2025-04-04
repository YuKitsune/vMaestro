﻿using Microsoft.Extensions.Logging;

namespace Maestro.Core.Configuration;

public class LoggingConfiguration(LogLevel logLevel, string outputPath)
{
    public LogLevel LogLevel { get; } = logLevel;
    public string OutputPath { get; } = outputPath;
}
