using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace IdleOptimizer.Components;

public abstract class FormDialogBase : ComponentBase
{
    [CascadingParameter] protected MudDialogInstance MudDialog { get; set; } = default!;
    
    protected MudForm? Form { get; set; }
    protected bool Success { get; set; }
    protected string[] Errors { get; set; } = [];

    protected virtual async Task<bool> ValidateFormAsync()
    {
        if (Form != null)
        {
            await Form.Validate();
            return Form.IsValid;
        }
        return false;
    }

    protected abstract Task OnSaveAsync();

    protected async Task SaveAsync()
    {
        var isValid = await ValidateFormAsync();
        if (isValid)
        {
            await OnSaveAsync();
        }
    }

    protected void Cancel()
    {
        MudDialog.Cancel();
    }
}