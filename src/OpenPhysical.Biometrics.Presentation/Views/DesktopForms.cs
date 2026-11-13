// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Domain-free form controls shared by Studio and the signing application.</summary>
public static class DesktopForms
{
    /// <summary>Arranges a readable form column.</summary>
    /// <param name="controls">Controls in reading order.</param>
    /// <returns>The form column.</returns>
    public static StackPanel Stack(params Control[] controls)
    {
        var panel = new StackPanel
        {
            Spacing = 16,
            MaxWidth = 760,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        };
        foreach (var control in controls)
            panel.Children.Add(control);
        return panel;
    }

    /// <summary>Creates wrapping explanatory text.</summary>
    /// <param name="text">Operator-facing text.</param>
    /// <returns>The wrapping label.</returns>
    public static TextBlock Text(string text) =>
        new() { Text = text, TextWrapping = TextWrapping.Wrap };

    /// <summary>Displays a live status property.</summary>
    /// <param name="path">Binding path on the inherited view model.</param>
    /// <returns>The bound wrapping label.</returns>
    public static TextBlock Status(string path)
    {
        var label = Text(string.Empty);
        label.Bind(TextBlock.TextProperty, new Binding(path));
        return label;
    }

    /// <summary>Creates a two-way text input.</summary>
    /// <param name="path">Binding path on the inherited view model.</param>
    /// <param name="watermark">Input hint shown while empty.</param>
    /// <param name="secret">Whether typed characters are masked.</param>
    /// <returns>The bound text input.</returns>
    public static TextBox Input(string path, string watermark, bool secret = false)
    {
        var input = new TextBox { Watermark = watermark, PasswordChar = secret ? '●' : '\0' };
        input.Bind(TextBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay });
        return input;
    }

    /// <summary>Labels an input or selection control.</summary>
    /// <param name="title">Visible field label.</param>
    /// <param name="control">Input or selection control.</param>
    /// <returns>The labelled field.</returns>
    public static Control Field(string title, Control control) =>
        new StackPanel { Spacing = 4, Children = { Text(title), control } };

    /// <summary>Labels a two-way text input.</summary>
    /// <param name="title">Visible field label.</param>
    /// <param name="path">Binding path on the inherited view model.</param>
    /// <param name="pin">Whether typed characters are masked.</param>
    /// <param name="watermark">Optional hint replacing the field label.</param>
    /// <returns>The labelled text input.</returns>
    public static Control Field(
        string title,
        string path,
        bool pin = false,
        string watermark = ""
    ) => Field(title, Input(path, string.IsNullOrEmpty(watermark) ? title : watermark, pin));

    /// <summary>Creates an accessible command button.</summary>
    /// <param name="title">Operator-facing action label.</param>
    /// <param name="path">Command binding path on the inherited view model.</param>
    /// <returns>The bound command button.</returns>
    public static Button Action(string title, string path)
    {
        var button = new Button { Content = title, MinHeight = 48 };
        button.Classes.Add("tonal");
        button.Bind(Button.CommandProperty, new Binding(path));
        return button;
    }

    /// <summary>Labels a selection whose value is the selected item itself.</summary>
    /// <typeparam name="T">Available item type.</typeparam>
    /// <param name="title">Visible selection label.</param>
    /// <param name="source">Items binding path.</param>
    /// <param name="selected">Selected-item binding path.</param>
    /// <param name="label">Human-readable item label.</param>
    /// <returns>The labelled item selection.</returns>
    public static Control Choice<T>(
        string title,
        string source,
        string selected,
        Func<T, string> label
    )
    {
        var choices = CreateChoices(source);
        choices.ItemTemplate = new FuncDataTemplate<T>(
            (value, _) => Text(value is null ? string.Empty : label(value))
        );
        choices.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding(selected) { Mode = BindingMode.TwoWay }
        );
        return Field(title, choices);
    }

    /// <summary>Creates a selection whose value is an index into the current item list.</summary>
    /// <param name="source">Items binding path.</param>
    /// <param name="selectedIndex">Selected-index binding path.</param>
    /// <param name="display">Optional property used to label each item.</param>
    /// <returns>The bound index selection.</returns>
    public static ComboBox IndexChoice(string source, string selectedIndex, string display = "")
    {
        var choices = CreateChoices(source);
        choices.Bind(
            SelectingItemsControl.SelectedIndexProperty,
            new Binding(selectedIndex) { Mode = BindingMode.TwoWay }
        );
        if (display.Length > 0)
            choices.ItemTemplate = new FuncDataTemplate<object>(
                (value, _) =>
                {
                    var text = Status(display);
                    text.DataContext = value;
                    return text;
                }
            );
        return choices;
    }

    /// <summary>Binds visibility without changing a control's own data context.</summary>
    /// <param name="control">Control being shown or hidden.</param>
    /// <param name="property">Boolean binding path on its inherited view model.</param>
    /// <returns>The same control with its visibility binding.</returns>
    public static Control Visible(Control control, string property)
    {
        control.Bind(Visual.IsVisibleProperty, new Binding(property));
        return control;
    }

    private static ComboBox CreateChoices(string source)
    {
        var choices = new ComboBox
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        };
        choices.Bind(ItemsControl.ItemsSourceProperty, new Binding(source));
        return choices;
    }
}
