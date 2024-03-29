﻿using Interpreter_lib.Tokenizer;

namespace Interpreter_lib.Parser
{
    public enum ERule
    {
        GROUP,

        // Arithmetic expressions
        ARITHMETIC_EXPRESSION,
        EXPRESSION_ATOM, SUBSEQUENT_EXPRESSION,

        SUM, SUBSEQUENT_SUM, SUBTRACT, SUBSEQUENT_SUBTRACT, MULTIPLY, SUBSEQUENT_MULTIPLY, DIVIDE, SUBSEQUENT_DIVIDE, POWER, SUBSEQUENT_POWER, MODULUS, SUBSEQUENT_MODULUS,
        FLOOR,

        // Logical expressions
        LOGICAL_EXPRESSION,
    }

    public class Rule : IRuleConfiguration,
        IRuleContinuationConfiguration,
        IRuleFrequencyConfiguration,
        IRuleTokenConfiguration,
        IRuleRuleConfiguration,
        ICloneable
    {
        // Rules
        private static List<Rule> _rules = new();

        // Used for traversing the tokens array.  
        public int _currentTokenIndex { get; private set; } = 0;
        private List<EToken> _currentTokensToMatch;
        private List<ERule> _currentRulesToMatch;
        private bool _hasMatchedW;
        private bool _isWSide = false;
        private bool _isTSide = false;
        private bool _lastToMatch = false; 

        // Used for additional behavior when creating the nodes.
        private bool _isHoisted = false;
        private bool _isExcluded = false;

        // Used for defining the rule.
        public ERule _rule { get; }
        private Action<IRuleConfiguration> _definition;

        // Data from which the syntax tree is created.
        private List<Token> _tokens;
        private Node _tree;

        public Rule(ERule rule, Action<IRuleConfiguration> definition)
        {
            _definition = definition;
            _rule = rule;
            _tree = new(_rule);
            _tokens = new();
            _hasMatchedW = false;
            _currentTokensToMatch = new();
            _currentRulesToMatch = new();
        }

        public Node Evaluate(List<Token> tokens)
        {
            Reset();
            _tokens = tokens;
            _definition(this);

            return _tree;
        }

        public void Reset()
        {
            _currentTokenIndex = 0;
            _isHoisted = false;
            _isExcluded = false;
            _tree = new(_rule);
            _hasMatchedW = false;
            _isWSide = false;
            _isTSide = false;
            _currentTokensToMatch.Clear();
            _currentRulesToMatch.Clear();
        }

        #region WITH 

        // Match the first token in the sequence
        IRuleTokenConfiguration IRuleConfiguration.WithT(params EToken[] tokens)
        {
            _isExcluded = false;
            _hasMatchedW = false;
            _currentRulesToMatch.Clear();
            _currentTokensToMatch.Clear();
            _currentTokensToMatch.AddRange(tokens);
            _isWSide = true;
            _isTSide = false;

            return this;
        }

        // Match the first rule in the sequence
        IRuleRuleConfiguration IRuleConfiguration.WithR(params ERule[] rules)
        {
            _isHoisted = false;
            _hasMatchedW = false;
            _currentRulesToMatch.Clear();
            _currentTokensToMatch.Clear();
            _currentRulesToMatch.AddRange(rules);
            _isWSide = true;
            _isTSide = false;

            return this;
        }

        #endregion

        #region THEN
        IRuleTokenConfiguration IRuleContinuationConfiguration.ThenT(params EToken[] token)
        {
            _isExcluded = false;
            _currentRulesToMatch.Clear();
            _currentTokensToMatch.Clear();
            _currentTokensToMatch.AddRange(token);
            _isWSide = false;
            _isTSide = true;

            return this;
        }

        IRuleRuleConfiguration IRuleContinuationConfiguration.ThenR(params ERule[] rule)
        {
            _isHoisted = false;
            _currentRulesToMatch.Clear();
            _currentTokensToMatch.Clear();
            _currentRulesToMatch.AddRange(rule);
            _isWSide = false;
            _isTSide = true;

            return this;
        }

        #endregion

        #region FREQUENCY

        // Match exactly once
        IRuleContinuationConfiguration IRuleFrequencyConfiguration.Once()
        {
            if (_tokens.Count == 0)
                return this;

            if (_isTSide && !_hasMatchedW)
                return this;

            if (_currentTokensToMatch.Count() > 0)
            {
                foreach (EToken token in _currentTokensToMatch)
                {
                    if (_tokens[_currentTokenIndex].Type == token)
                    {
                        AddToTree(_tokens[_currentTokenIndex]);
                        _currentTokenIndex++;

                        if (_isWSide)
                            _hasMatchedW = true;

                        break;
                    }
                }
            }
            else if (_currentRulesToMatch.Count() > 0)
            {
                List<Rule> currentRules = GetRules(_currentRulesToMatch);
                Node node;
                var ok = false;

                for (int i = 0; i < currentRules.Count; i++)
                {
                    Rule currentRule = currentRules[i];
                    if(i == currentRules.Count - 1)
                        currentRule._lastToMatch = true;

                    if (_currentTokenIndex > 0)
                        node = currentRule.Evaluate(_tokens.Skip(_currentTokenIndex).ToList());
                    else
                        node = currentRule.Evaluate(_tokens);

                    if (!node.IsEmpty)
                    {
                        AddToTree(node);
                        _currentTokenIndex += currentRule._currentTokenIndex;
                        ok = true;
                        break;
                    }
                }

                if (_hasMatchedW && !ok && _lastToMatch)
                    throw new ParsingException(this, "Rule has matched less than once.");

                if (ok && _isWSide)
                    _hasMatchedW = true;
            }

            return this;
        }

        // Match at least once
        IRuleContinuationConfiguration IRuleFrequencyConfiguration.AtLeastOnce()
        {
            if (_tokens.Count == 0)
                return this;

            if (_isTSide && !_hasMatchedW)
                return this;

            bool ok = false;

            if (_currentTokensToMatch.Count() > 0)
            {
                foreach (EToken token in _currentTokensToMatch)
                {
                    while (_tokens[_currentTokenIndex].Type == token)
                    {
                        AddToTree(_tokens[_currentTokenIndex]);
                        _currentTokenIndex++;
                        ok = true;
                    }
                }

                if (ok && _isWSide)
                    _hasMatchedW = true;
                 
                if (_hasMatchedW && !ok && _lastToMatch)
                    throw new ParsingException(this, "Token has matched less than once.");
            }
            else if (_currentRulesToMatch.Count() > 0)
            {
                List<Rule> currentRules = GetRules(_currentRulesToMatch);
                Node node;

                for (int i = 0; i < currentRules.Count; i++)
                {
                    Rule currentRule = currentRules[i];
                    if (i == currentRules.Count - 1)
                        currentRule._lastToMatch = true;

                    do
                    {
                        if (_currentTokenIndex > 0)
                            node = currentRule.Evaluate(_tokens.Skip(_currentTokenIndex).ToList());
                        else
                            node = currentRule.Evaluate(_tokens);

                        if (!node.IsEmpty)
                        {
                            AddToTree(node);
                            _currentTokenIndex += currentRule._currentTokenIndex;
                            ok = true;
                        }
                    } while (!node.IsEmpty);
                }

                if (_hasMatchedW && !ok && _lastToMatch)
                    throw new ParsingException(this, "Rule has matched less than once.");

                if (ok && _isWSide)
                    _hasMatchedW = true;
            }

            return this;
        }

        // Match zero or one time at most
        IRuleContinuationConfiguration IRuleFrequencyConfiguration.AtMostOnce()
        {
            if (_tokens.Count == 0)
                return this;

            if (_isTSide && !_hasMatchedW)
                return this;

            if (_currentTokensToMatch.Count() > 0)
            {
                var ok = false;
                foreach (EToken token in _currentTokensToMatch)
                {
                    if (_tokens[_currentTokenIndex].Type == token)
                    {
                        AddToTree(_tokens[_currentTokenIndex]);
                        _currentTokenIndex++;
                        ok = true;

                        if (_tokens[_currentTokenIndex].Type == token && _hasMatchedW && _lastToMatch)
                            throw new ParsingException(this, "Token has matched more than once.");
                    }

                    if (ok)
                        break;
                }

                if(ok && _isWSide)
                    _hasMatchedW = true;
            }
            else if (_currentRulesToMatch.Count() > 0)
            {
                List<Rule> currentRules = GetRules(_currentRulesToMatch);
                Node node;
                var ok = false;
                foreach (Rule currentRule in currentRules)
                {
                    if (_currentTokenIndex > 0)
                        node = currentRule.Evaluate(_tokens.Skip(_currentTokenIndex).ToList());
                    else
                        node = currentRule.Evaluate(_tokens);

                    if (!node.IsEmpty)
                    {
                        AddToTree(node);
                        _currentTokenIndex += currentRule._currentTokenIndex;
                        ok = true;
                        break;
                    }
                }

                if (ok)
                {
                    foreach (Rule currentRule in currentRules)
                    {
                        if (_currentTokenIndex > 0)
                            node = currentRule.Evaluate(_tokens.Skip(_currentTokenIndex).ToList());
                        else
                            node = currentRule.Evaluate(_tokens);

                        if (!node.IsEmpty && _hasMatchedW && _lastToMatch)
                        {
                            throw new ParsingException(this, "Rule has matched more than once.");
                        }
                    }
                }

                if (ok && _isWSide)
                    _hasMatchedW = true;
            }

            return this;
        }

        // Match zero or more times
        IRuleContinuationConfiguration IRuleFrequencyConfiguration.ZeroOrMore()
        {
            if (_tokens.Count == 0)
                return this;

            if (_isTSide && !_hasMatchedW)
                return this;

            if (_currentTokensToMatch.Count() > 0)
            {
                var ok = false; 
                foreach (EToken token in _currentTokensToMatch)
                {
                    while (_tokens[_currentTokenIndex].Type == token)
                    {
                        AddToTree(_tokens[_currentTokenIndex]);
                        _currentTokenIndex++;
                        ok = true;
                    }
                }

                if (ok && _isWSide)
                    _hasMatchedW = true;
            }
            else if (_currentRulesToMatch.Count() > 0)
            {
                List<Rule> currentRules = GetRules(_currentRulesToMatch);
                Node node;
                var ok = false; 
                foreach (Rule currentRule in currentRules)
                {
                    do
                    {
                        if (_currentTokenIndex > 0)
                            node = currentRule.Evaluate(_tokens.Skip(_currentTokenIndex).ToList());
                        else
                            node = currentRule.Evaluate(_tokens);

                        if (!node.IsEmpty)
                        {
                            AddToTree(node);
                            _currentTokenIndex += currentRule._currentTokenIndex;
                            ok = true;
                        }
                    } while (!node.IsEmpty);
                }

                if (ok && _isWSide)
                    _hasMatchedW = true;
            }

            return this;
        }

        #endregion

        #region ADDITIONAL BEHAVIOR

        // This can be applied to token elements (such as Add) and will cause that token to be matched
        // but not included in the resultant tree
        IRuleTokenConfiguration IRuleTokenConfiguration.Exclude()
        {
            _isExcluded = true;

            return this;
        }

        // This can be applied to rule elements (such as SubsequentSum) and will cause the element to be replaced
        // by it's content causing the content to be hoisted up a level
        IRuleRuleConfiguration IRuleRuleConfiguration.Hoist()
        {
            _isHoisted = true;

            return this;
        }

        #endregion

        #region TREE MANIPULATION
        private void AddToTree(Token token)
        {
            if (!_isExcluded)
                _tree.Add(token);
        }

        private void AddToTree(Node node)
        {
            if (_isHoisted || node.GetRule() == _rule)
            {
                _tree.Add(node.GetNodes());
                _tree.Add(node.GetTokens());
            }
            else
            {
                _tree.Add(node);
            }
        }
        #endregion

        #region STATIC METHODS
        public static List<Rule> GetRules()
        {
            return _rules;
        }

        public static List<Rule> GetRules(ERule rule)
        {
            return new List<Rule>(_rules.Where(r => r._rule == rule).Select(r => (Rule)r.Clone()).ToList());
        }

        public static List<Rule> GetRules(List<ERule> rules)
        {
            var list = new List<Rule>();
            foreach (ERule rule in rules)
            {
                list.AddRange(_rules.Where(r => r._rule == rule).Select(r => (Rule)r.Clone()).ToList());
            }

            return list;
        }

        public static void AddRule(Rule rule)
        {
            _rules.Add(rule);
        }

        #endregion

        #region OTHER

        public object Clone()
        {
            return new Rule(_rule, _definition);
        }

        #endregion
    }
}
