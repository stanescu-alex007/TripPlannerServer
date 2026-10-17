using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TripPlanner.WEB;

/// <summary>
/// Stops model binding from reading the form before the action runs. Upload actions read
/// it themselves (<c>ReadUploadAsync</c>) so they can answer an oversized body with
/// IMAGE_TOO_LARGE; left in place, the framework reads it first and turns the same
/// failure into a generic VALIDATION_FAILED.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        context.ValueProviderFactories.RemoveType<FormValueProviderFactory>();
        context.ValueProviderFactories.RemoveType<FormFileValueProviderFactory>();
        context.ValueProviderFactories.RemoveType<JQueryFormValueProviderFactory>();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
