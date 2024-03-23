﻿using Interpreter_lib.Tokenizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interpreter_lib.Parser
{
    public enum ERule
    {
        SUM,
        SUBSEQUENTSUM,
    }

    public class Rule : IRuleConfiguration, IRuleContinuationConfiguration, IRuleFrequencyConfiguration, IRuleTokenConfiguration, IRuleRuleConfiguration
    {
        // Rules
        private static List<Rule> _rules = new();

        // Used for traversing the tokens array.  
        private int _currentTokenIndex = 0;
        private bool _hasPassedWithMethod;
        private EToken? _currentTokenToMatch;
        private ERule? _currentRuleToMatch;

        // Used for additional behavior when creating the nodes.
        private bool _isHoisted = false;
        private bool _isExcluded = false;

        // Used for defining the rule.
        private ERule _rule { get; }
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
            _hasPassedWithMethod = false;
            // Example:
            // new Rule(o => o
            //         .WithT(EToken.REPEAT).Exclude().Once()
            //         .ThenR(logicalExpressionRule).Hoist().Once()
            //      );
        }

        public Node Evaluate(List<Token> tokens)
        {
            _tokens = tokens;
            _definition(this);

            return _tree;
        }

        private void Reset()
        {
            _currentTokenIndex = 0;
            _isHoisted = false;
            _isExcluded = false;
            _tokens.Clear();
            _tree = new(_rule);
            _hasPassedWithMethod = false;
        }

        #region WITH 

        // Match the first token in the sequence
        IRuleTokenConfiguration IRuleConfiguration.WithT(EToken token)
        {
            _isExcluded = false;
            _hasPassedWithMethod = false;
            _currentTokenToMatch = token;
            _currentRuleToMatch = null;

            return this;
        }

        // Match the first rule in the sequence
        IRuleRuleConfiguration IRuleConfiguration.WithR(ERule rule)
        {
            _isHoisted = false;
            _hasPassedWithMethod = false;
            _currentRuleToMatch = rule;
            _currentTokenToMatch = null;

            return this;
        }

        #endregion

        #region THEN
        IRuleTokenConfiguration IRuleContinuationConfiguration.ThenT(EToken token)
        {
            _isExcluded = false;
            _currentTokenToMatch = token;
            _currentRuleToMatch = null;

            return this;
        }

        IRuleRuleConfiguration IRuleContinuationConfiguration.ThenR(ERule rule)
        {
            _isHoisted = false;
            _currentRuleToMatch = rule;
            _currentTokenToMatch = null;

            return this;
        }

        #endregion

        #region FREQUENCY

        // Match exactly once
        IRuleContinuationConfiguration IRuleFrequencyConfiguration.Once()
        {
            if (_currentTokenToMatch != null && _tokens[_currentTokenIndex].Type == _currentTokenToMatch)
            {
                AddToTree(_tokens[_currentTokenIndex]);
                _hasPassedWithMethod = true;
                _currentTokenIndex++;
            }
            else if (_currentRuleToMatch != null)
            {
                Rule currentRule = GetRule(_currentRuleToMatch.Value);
                Node node;
                if (_currentTokenIndex > 0)
                    node = currentRule.Evaluate(_tokens.Skip(_currentTokenIndex).ToList());
                else
                    node = currentRule.Evaluate(_tokens);

                if (!node.IsEmpty)
                {
                    AddToTree(node);
                    _currentTokenIndex += currentRule._currentTokenIndex;
                    currentRule.Reset();
                    _hasPassedWithMethod = true;
                }
                else if (_hasPassedWithMethod)
                {
                    throw new ParsingException(this, "Rule matched less than once.");
                }
            }
            else if (_hasPassedWithMethod)
            {
                throw new ParsingException(this, "Token matched more than once.");
            }

            return this;
        }

        // Match at least once
        IRuleContinuationConfiguration IRuleFrequencyConfiguration.AtLeastOnce()
        {
            bool ok = false;

            if (_currentTokenToMatch != null)
            {
                while (_tokens[_currentTokenIndex].Type == _currentTokenToMatch)
                {
                    AddToTree(_tokens[_currentTokenIndex]);
                    _currentTokenToMatch++;

                    ok = true;
                }

                if (_hasPassedWithMethod && !ok)
                    throw new ParsingException(this, "Token matched less than once.");
            }
            else if (_currentRuleToMatch != null)
            {
                Rule currentRule = GetRule(_currentRuleToMatch.Value);
                Node node;

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
                        currentRule.Reset();
                        ok = true;
                    }
                    else if (_hasPassedWithMethod && !ok)
                    {
                        throw new ParsingException(this, "Rule matched less than once.");
                    }
                } while (!node.IsEmpty);
            }

            _hasPassedWithMethod = true;
            return this;
        }

        // Match zero or one time at most
        IRuleContinuationConfiguration IRuleFrequencyConfiguration.AtMostOnce()
        {
            if (_currentTokenToMatch != null)
            {
                if (_tokens[_currentTokenIndex].Type == _currentTokenToMatch)
                {
                    AddToTree(_tokens[_currentTokenIndex]);
                    _currentTokenToMatch++;

                    if (_tokens[_currentTokenIndex].Type == _currentTokenToMatch && _hasPassedWithMethod)
                        throw new ParsingException(this, "Token matched more than once.");
                }
            }
            else if (_currentRuleToMatch != null)
            {
                Rule currentRule = GetRule(_currentRuleToMatch.Value);
                Node node;

                if (_currentTokenIndex > 0)
                    node = currentRule.Evaluate(_tokens.Skip(_currentTokenIndex).ToList());
                else
                    node = currentRule.Evaluate(_tokens);

                if (!node.IsEmpty)
                {
                    AddToTree(node);
                    _currentTokenIndex += currentRule._currentTokenIndex;
                    currentRule.Reset();
                    if (_currentTokenIndex > 0)
                        node = currentRule.Evaluate(_tokens.Skip(_currentTokenIndex).ToList());
                    else
                        node = currentRule.Evaluate(_tokens);

                    if (!node.IsEmpty && _hasPassedWithMethod)
                        throw new ParsingException(this, "Rule matched more than once.");
                }
            }

            _hasPassedWithMethod = true;
            return this;
        }

        // Match zero or more times
        IRuleContinuationConfiguration IRuleFrequencyConfiguration.ZeroOrMore()
        {
            if (_currentTokenToMatch != null)
            {
                while (_tokens[_currentTokenIndex].Type == _currentTokenToMatch)
                {
                    AddToTree(_tokens[_currentTokenIndex]);
                    _currentTokenToMatch++;
                }
            }
            else if (_currentRuleToMatch != null)
            {
                Rule currentRule = GetRule(_currentRuleToMatch.Value);
                Node node;

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
                        currentRule.Reset();
                    }
                } while (!node.IsEmpty);
            }

            _hasPassedWithMethod = true;
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
            if (_isHoisted)
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
        public static Rule GetRule(ERule rule)
        {
            return _rules.First(r => r._rule == rule);
        }

        public static List<Rule> GetRules()
        {
            return _rules;
        }

        public static void AddRule(Rule rule)
        {
            _rules.Add(rule);
        }
        #endregion
    }
}
