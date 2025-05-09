using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DuplicateAssistant.ViewModels;

namespace DuplicateAssistant;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        string? name = data?.GetType().FullName!.Replace("ViewModel", "View");
        if (name != null)
        {
            Type? type = Type.GetType(name);

            if (type != null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }
        }
        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}