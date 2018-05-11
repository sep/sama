using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace sama.Models
{
    public class PemEncodedDataAttribute : ValidationAttribute
    {
        private string _sectionName;

        public PemEncodedDataAttribute(string sectionName)
        {
            _sectionName = sectionName;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var certString = value as string;
            if (certString == null)
            {
                return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
            }

            if (string.IsNullOrWhiteSpace(certString))
            {
                // Can only be whitespace here, which is fine
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
