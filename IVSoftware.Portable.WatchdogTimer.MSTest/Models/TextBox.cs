namespace IVSoftware.Portable.MSTest.Models
{
    class TextBox
    {
        public string Text
        {
            get => _text;
            set
            {
                if (!Equals(_text, value))
                {
                    _text = value;
                    OnTextChanged();
                }
            }
        }
        string _text = string.Empty;

        protected virtual void OnTextChanged() => TextChanged?.Invoke(this, EventArgs.Empty);

        public event EventHandler? TextChanged;
        public virtual void Commit() { }

        public void Clear() => Text = string.Empty;
    }
}
