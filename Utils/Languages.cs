﻿using Interpreter_lib.Tokenizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interpreter_lib.Utils
{
    public static class Languages
    {
        public static TokensLanguage english = new();
        public static TokensLanguage romanian = new();

        static Languages()
        {
            // TODO: Get the languages from a file at runtime. (That way users can make custom languages easily if they want to)
            romanian.READ = "citeste";
            romanian.WRITE = "scrie";
            romanian.IF = "daca";
            romanian.THEN = "atunci";
            romanian.ELSE = "altfel";
            romanian.WHILE = "cat timp";
            romanian.DO = "executa";
            romanian.REPEAT = "repeta";
            romanian.UNTIL = "pana cand";
            romanian.FOR = "pentru";
        }
    }
}
