using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace sama.Models
{
    public class PemEncodedDataAttribute : ValidationAttribute
    {
        private readonly string _sectionName;

        public PemEncodedDataAttribute(string sectionName)
        {
            _sectionName = sectionName;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var certString = value as string;
            if (value != null && certString == null)
            {
                // It's not a string... somehow?
                return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
            }

            if (string.IsNullOrWhiteSpace(certString))
            {
                return ValidationResult.Success;
            }

            if (Regex.IsMatch(certString, @".*-----BEGIN " + _sectionName + @"-----.*-----END " + _sectionName + @"-----.*", RegexOptions.Singleline))
            {
                return ValidationResult.Success;
            }
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
        }
    }
}
