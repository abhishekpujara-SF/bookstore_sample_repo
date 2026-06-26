using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bookstore.Common
{
    /// <summary>
    /// Extension methods for string manipulation.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Converts a string to lowercase.
        /// </summary>
        /// <param name="input">The string to convert.</param>
        /// <returns>The lowercase string.</returns>
        public static string ToLowerCase(this string input)
        {
            return input?.ToLower();
        }

        /// <summary>
        /// Converts a string to camel case.
        /// </summary>
        /// <param name="input">The string to convert.</param>
        /// <returns>The camel case string.</returns>
        public static string ToCamelCase(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string[] words = Regex.Split(input.Trim(), @"[\s_-]");
            StringBuilder result = new StringBuilder(words[0].ToLower());

            for (int i = 1; i < words.Length; i++)
            {
                if (!string.IsNullOrEmpty(words[i]))
                {
                    result.Append(char.ToUpper(words[i][0]));
                    if (words[i].Length > 1)
                        result.Append(words[i].Substring(1).ToLower());
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Converts a string to pascal case.
        /// </summary>
        /// <param name="input">The string to convert.</param>
        /// <returns>The pascal case string.</returns>
        public static string ToPascalCase(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string[] words = Regex.Split(input.Trim(), @"[\s_-]");
            StringBuilder result = new StringBuilder();

            foreach (string word in words)
            {
                if (!string.IsNullOrEmpty(word))
                {
                    result.Append(char.ToUpper(word[0]));
                    if (word.Length > 1)
                        result.Append(word.Substring(1).ToLower());
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Humanizes a string by inserting spaces before capital letters and trimming.
        /// </summary>
        /// <param name="input">The string to humanize.</param>
        /// <returns>The humanized string.</returns>
        public static string Humanize(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Insert a space before each capital letter that is not the first character
            string result = Regex.Replace(input, "(?<!^)([A-Z])", " $1");
            
            // Replace underscores and hyphens with spaces
            result = Regex.Replace(result, "[_-]", " ");
            
            // Remove extra spaces and trim
            result = Regex.Replace(result, @"\s+", " ").Trim();
            
            // Capitalize the first letter
            if (result.Length > 0)
            {
                result = char.ToUpper(result[0]) + result.Substring(1);
            }

            return result;
        }
    }
}