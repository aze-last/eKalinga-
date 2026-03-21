namespace AttendanceShiftingManagement.Helpers
{
    public class SelectionOption<T>
    {
        public string Label { get; set; } = string.Empty;
        public T? Value { get; set; }

        public override string ToString()
        {
            return Label;
        }
    }
}
