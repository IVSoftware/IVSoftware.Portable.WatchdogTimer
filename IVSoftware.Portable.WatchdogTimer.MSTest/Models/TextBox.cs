namespace IVSoftware.Portable.MSTest.Models
{
    class TextBox
    {
        public string Text { get; set; } = string.Empty;
        public event EventHandler? TextChanged;
        public virtual void Commit() { }
    }
}
