using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Areas.Identity.Pages.Account
{
    public class ConditionalRequiredAttribute : ValidationAttribute, IClientModelValidator
    {
        private readonly string _comparisonValue;

        public ConditionalRequiredAttribute(string comparisonValue)
        {
            _comparisonValue = comparisonValue;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var emailProperty = validationContext.ObjectType.GetProperty("Email");
            if (emailProperty != null)
            {
                var emailValue = (string)emailProperty.GetValue(validationContext.ObjectInstance);

                if (!string.IsNullOrEmpty(emailValue) && !emailValue.EndsWith(_comparisonValue, StringComparison.OrdinalIgnoreCase))
                {
                    if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                    {
                        return new ValidationResult(ErrorMessage ?? "The Company field is required.");
                    }
                }
            }

            return ValidationResult.Success;
        }

        public void AddValidation(ClientModelValidationContext context)
        {
            MergeAttribute(context.Attributes, "data-val", "true");
            MergeAttribute(context.Attributes, "data-val-conditionalrequired", ErrorMessage);

            var comparisonValue = _comparisonValue.ToLower() == "true" ? "true" : "false";
            MergeAttribute(context.Attributes, "data-val-conditionalrequired-value", comparisonValue);
        }

        private void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
        {
            if (attributes.ContainsKey(key))
            {
                return;
            }

            attributes.Add(key, value);
        }
    }
}
