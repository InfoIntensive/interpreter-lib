﻿using Interpreter_lib.Tokenizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interpreter_lib.Parser
{
    // Inspiration from:
    // https://gwerren.com/Blog/Posts/simpleCSharpParser

    public interface IRuleConfiguration
    {
        IRuleTokenConfiguration WithT(EToken token);
        IRuleRuleConfiguration WithR(ERule rule);
    }

    public interface IRuleContinuationConfiguration
    {
        IRuleTokenConfiguration ThenT(EToken token);
        IRuleRuleConfiguration ThenR(ERule rule);
    }

    public interface IRuleFrequencyConfiguration
    {
        IRuleContinuationConfiguration Once();
        IRuleContinuationConfiguration AtMostOnce();
        IRuleContinuationConfiguration AtLeastOnce();
        IRuleContinuationConfiguration ZeroOrMore();
    }

    public interface IRuleTokenConfiguration : IRuleFrequencyConfiguration
    {
        IRuleTokenConfiguration Exclude();
    }

    public interface IRuleRuleConfiguration : IRuleFrequencyConfiguration
    {
        IRuleRuleConfiguration Hoist();
    }


}
