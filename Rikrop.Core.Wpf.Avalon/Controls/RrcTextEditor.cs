using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Rikrop.Core.Wpf.Commands;
using Rikrop.Core.Wpf.Helpers;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;

namespace Rikrop.Core.Wpf.Avalon.Controls
{
    public class RrcTextEditor : TextEditor
    {
        public static readonly DependencyProperty HighlightedWordsProperty = DependencyProperty.Register(
            "HighlightedWords",
            typeof (IEnumerable<string>),
            typeof (RrcTextEditor),
            new PropertyMetadata(default(IEnumerable<string>)));

        public static readonly DependencyProperty HighlightedWordsBackgroundProperty = DependencyProperty.Register(
            "HighlightedWordsBackground",
            typeof (Brush),
            typeof (RrcTextEditor),
            new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0x44, 0, 0, 255))));

        public static readonly DependencyProperty HighlightedTextProperty = DependencyProperty.Register(
            "HighlightedText",
            typeof (string),
            typeof (RrcTextEditor),
            new PropertyMetadata(default(string)));

        public static readonly DependencyProperty HighlightedTextBackgroundProperty = DependencyProperty.Register(
            "HighlightedTextBackground",
            typeof (Brush),
            typeof (RrcTextEditor),
            new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0xAA, 255, 165, 0))));

        public static readonly DependencyProperty AutoScrollToHighlightedTextProperty = DependencyProperty.Register(
            "AutoScrollToHighlightedText",
            typeof (bool),
            typeof (RrcTextEditor),
            new PropertyMetadata(default(bool)));

        public static readonly DependencyProperty DocumentTextProperty = DependencyProperty.Register(
            "DocumentText",
            typeof (string),
            typeof (RrcTextEditor),
            new PropertyMetadata(default(string)));

        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
            "IsActive",
            typeof (bool),
            typeof (RrcTextEditor),
            new PropertyMetadata(default(bool), IsActiveChangedCallback));

        private bool _clearSelected = true;
        private RelayCommand _copySelectedCommand;
        private RelayCommand _copyAllCommand;

        public IEnumerable<string> HighlightedWords
        {
            get { return (IEnumerable<string>) GetValue(HighlightedWordsProperty); }
            set { SetValue(HighlightedWordsProperty, value); }
        }

        public Brush HighlightedWordsBackground
        {
            get { return (Brush) GetValue(HighlightedWordsBackgroundProperty); }
            set { SetValue(HighlightedWordsBackgroundProperty, value); }
        }

        public string HighlightedText
        {
            get { return (string) GetValue(HighlightedTextProperty); }
            set { SetValue(HighlightedTextProperty, value); }
        }

        public Brush HighlightedTextBackground
        {
            get { return (Brush) GetValue(HighlightedTextBackgroundProperty); }
            set { SetValue(HighlightedTextBackgroundProperty, value); }
        }

        public bool AutoScrollToHighlightedText
        {
            get { return (bool) GetValue(AutoScrollToHighlightedTextProperty); }
            set { SetValue(AutoScrollToHighlightedTextProperty, value); }
        }

        public string DocumentText
        {
            get { return (string) GetValue(DocumentTextProperty); }
            set { SetValue(DocumentTextProperty, value); }
        }

        public bool IsActive
        {
            get { return (bool) GetValue(IsActiveProperty); }
            set { SetValue(IsActiveProperty, value); }
        }

        public ICommand CopySelectedCommand
        {
            get
            {
                if (_copySelectedCommand == null)
                {
                    _copySelectedCommand = new RelayCommandBuilder(CopySelected).AddCanExecute(CanCopySelected).CreateCommand();
                }
                return _copySelectedCommand;
            }
        }

        public ICommand CopyAllCommand
        {
            get
            {
                if (_copyAllCommand == null)
                {
                    _copyAllCommand = new RelayCommandBuilder(CopyAll).AddCanExecute(CanCopyAll).CreateCommand();
                }
                return _copyAllCommand;
            }
        }

        static RrcTextEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof (RrcTextEditor), new FrameworkPropertyMetadata(typeof (RrcTextEditor)));            
        }

        public RrcTextEditor()
        {
            ScrollViewerHelper.SetMouseWheelHelp(this, true);

            PreviewMouseRightButtonDown += ApiTextEditorPreviewMouseRightButtonDown;
            Loaded += ApiTextEditorLoaded;
        }

        void ApiTextEditorLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ApiTextEditorLoaded;
            InitializeSyntaxDefinition();
        }

        void ApiTextEditorPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            InvalidateContextMenu();
        }

        public static void IsActiveChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (RrcTextEditor) d;
            if ((bool) e.NewValue)
            {
                ctrl.Focus();
            }
        }


        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.Property == HighlightedWordsProperty ||
                e.Property == HighlightedTextProperty ||
                e.Property == HighlightedWordsBackgroundProperty ||
                e.Property == HighlightedTextBackgroundProperty)
            {
                InitializeSyntaxDefinition();
                if (e.Property == HighlightedTextProperty && AutoScrollToHighlightedText)
                {
                    ScrollToHighlightedText();
                }
            }
            else if (e.Property == DocumentTextProperty)
            {
                Document = new TextDocument(DocumentText ?? string.Empty);
            }
        }

        private ContextMenu _copyMenu;
        private void InvalidateContextMenu()
        {
            if (SelectionLength == 0)
            {
                if (ContextMenu != null)
                {
                    _copyMenu = ContextMenu;
                    ContextMenu = null;
                }
            }
            else
            {
                if (_copyMenu != null)
                    ContextMenu = _copyMenu;
            }
        }

        protected override void OnPreviewLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            if (_clearSelected)
            {
                Select(0, 0);
            }
            else
            {
                _clearSelected = true;
            }

            base.OnPreviewLostKeyboardFocus(e);
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            _clearSelected = false;
            e.Handled = false;
        }

        private void ScrollToHighlightedText()
        {
            if (Document != null)
            {
                int index = Document.Text.IndexOf(HighlightedText, StringComparison.InvariantCultureIgnoreCase);
                if (index != -1)
                {
                    TextLocation location = Document.GetLocation(index);
                    ScrollTo(location.Line, location.Column);
                }
            }
        }

        private void InitializeSyntaxDefinition()
        {
            if(HighlightedWords == null && string.IsNullOrWhiteSpace(HighlightedText))
                SyntaxHighlighting = new ForegroundHighlight(Foreground);
            else
                SyntaxHighlighting = new ApiHighlightDefinition(HighlightedWords, HighlightedWordsBackground, HighlightedText, HighlightedTextBackground);
        }

        private bool CanCopySelected()
        {
            return SelectionLength > 0;
        }

        private void CopySelected()
        {
            ClipboardHelper.SetData(DataFormats.UnicodeText, SelectedText);
        }

        private bool CanCopyAll()
        {
            return SelectionLength > 0;
        }

        private void CopyAll()
        {
            ClipboardHelper.SetData(DataFormats.UnicodeText, Text);
        }
    }

    internal class ApiHighlightDefinition : IHighlightingDefinition
    {
        public string Name
        {
            get { return "ApiHighlighter"; }
        }

        public HighlightingRuleSet MainRuleSet { get; private set; }

        public IEnumerable<HighlightingColor> NamedHighlightingColors
        {
            get { return Enumerable.Empty<HighlightingColor>(); }
        }

        public ApiHighlightDefinition(IEnumerable<string> highlightedWords, Brush highlightedWordsBackground, string highlightedText, Brush highlightedTextBackground)
        {
            MainRuleSet = new HighlightingRuleSet();
            if (highlightedWords != null && highlightedWords.Any())
            {
                MainRuleSet.Rules.Add(CreateHighlightedRule(highlightedWords, highlightedWordsBackground, true));
            }
            if (!string.IsNullOrWhiteSpace(highlightedText))
            {
                MainRuleSet.Rules.Add(CreateHighlightedRule(new[] {highlightedText}, highlightedTextBackground, false));
            }
        }

        private static bool IsSimpleWord(string word)
        {
            return char.IsLetterOrDigit(word[0]) && char.IsLetterOrDigit(word, word.Length - 1);
        }

        public HighlightingRuleSet GetNamedRuleSet(string name)
        {
            return MainRuleSet;
        }

        public HighlightingColor GetNamedColor(string name)
        {
            return null;
        }

        private HighlightingRule CreateHighlightedRule(IEnumerable<string> highlightedWords, Brush color, bool searchAsWords)
        {
            var keyWordRegex = new StringBuilder();
            // We can use "\b" only where the keyword starts/ends with a letter or digit, otherwise we don't
            // highlight correctly. (example: ILAsm-Mode.xshd with ".maxstack" keyword)
            if (highlightedWords.All(IsSimpleWord))
            {
                keyWordRegex.Append(string.Format(@"{0}(?>", searchAsWords
                                                                 ? @"\b"
                                                                 : string.Empty));
                // (?> = atomic group
                // atomic groups increase matching performance, but we
                // must ensure that the keywords are sorted correctly.
                // "\b(?>in|int)\b" does not match "int" because the atomic group captures "in".
                // To solve this, we are sorting the keywords by descending length.
                int i = 0;
                foreach (string keyword in highlightedWords.OrderByDescending(w => w.Length))
                {
                    if (i++ > 0)
                    {
                        keyWordRegex.Append('|');
                    }
                    keyWordRegex.Append(Regex.Escape(keyword).Replace("\\ ", "([^\\w]|[_])*"));
                }
                keyWordRegex.Append(string.Format(@"){0}", searchAsWords
                                                               ? @"\b"
                                                               : string.Empty));
            }
            else
            {
                keyWordRegex.Append('(');
                int i = 0;
                foreach (string keyword in highlightedWords)
                {
                    if (i++ > 0)
                    {
                        keyWordRegex.Append('|');
                    }
                    if (char.IsLetterOrDigit(keyword[0]) && searchAsWords)
                    {
                        keyWordRegex.Append(@"\b");
                    }
                    keyWordRegex.Append(Regex.Escape(keyword));
                    if (char.IsLetterOrDigit(keyword[keyword.Length - 1]) && searchAsWords)
                    {
                        keyWordRegex.Append(@"\b");
                    }
                }
                keyWordRegex.Append(')');
            }
            return new HighlightingRule
                       {
                           Color = new HighlightingColor
                                       {
                                           Background = new SimpleHighlightingBrush(color)
                                       },
                           Regex = CreateRegex(keyWordRegex.ToString())
                       };
        }

        private Regex CreateRegex(string regex)
        {
            return new Regex(regex, RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);
        }
    }

    internal class ForegroundHighlight : IHighlightingDefinition
    {
        public ForegroundHighlight(Brush foreground)
        {
            MainRuleSet = new HighlightingRuleSet();
            MainRuleSet.Rules.Add(new HighlightingRule() { Regex = new Regex(".+"), Color = new HighlightingColor() { Foreground = new SimpleHighlightingBrush(foreground) } });
        }

        public HighlightingRuleSet GetNamedRuleSet(string name)
        {
            return MainRuleSet;
        }

        public HighlightingColor GetNamedColor(string name)
        {
            return null;
        }

        public string Name { get; private set; }
        public HighlightingRuleSet MainRuleSet { get; private set; }
        public IEnumerable<HighlightingColor> NamedHighlightingColors { get; private set; }
    }

    [Serializable]
    internal sealed class SimpleHighlightingBrush : HighlightingBrush, ISerializable
    {
        private readonly Brush _brush;

        public SimpleHighlightingBrush(Brush brush)
        {
            brush.Freeze();
            _brush = brush;
        }

        public SimpleHighlightingBrush(Color color) : this(new SolidColorBrush(color))
        {
        }

        private SimpleHighlightingBrush(SerializationInfo info, StreamingContext context)
        {
            _brush = new SolidColorBrush((Color) ColorConverter.ConvertFromString(info.GetString("color")));
            _brush.Freeze();
        }

        public override Brush GetBrush(ITextRunConstructionContext context)
        {
            return _brush;
        }

        public override string ToString()
        {
            return _brush.ToString();
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("color", _brush.ToString(CultureInfo.InvariantCulture));
        }
    }
}