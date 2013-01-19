﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ably
{
    internal static class StringExtensions
    {
        public static bool IsNotEmpty(this string text)
        {
            return string.IsNullOrEmpty(text) == false;
        }

        public static bool IsEmpty(this string text)
        {
            return string.IsNullOrEmpty(text);
        }
    }
}