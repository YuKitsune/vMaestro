﻿using Maestro.Core.Dtos.Configuration;

namespace Maestro.Core.Configuration;

public interface ISeparationConfigurationProvider
{
    public SeparationRuleConfiguration[] GetSeparationRules();
}
