namespace AttendanceShiftingManagement.Helpers
{
    public enum WalkthroughPlacement
    {
        Auto,
        Top,
        Bottom,
        Left,
        Right
    }

    public sealed class WalkthroughStepDefinition : ObservableObject
    {
        private int _stepNumber;
        private string _title = string.Empty;
        private string _instruction = string.Empty;
        private string _actionHint = string.Empty;
        private string _targetElementName = string.Empty;
        private WalkthroughPlacement _preferredPlacement = WalkthroughPlacement.Auto;
        private double _spotlightPadding = 8.0;
        private double _spotlightCornerRadius = 10.0;

        public int StepNumber
        {
            get => _stepNumber;
            set => SetProperty(ref _stepNumber, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Instruction
        {
            get => _instruction;
            set => SetProperty(ref _instruction, value);
        }

        public string ActionHint
        {
            get => _actionHint;
            set => SetProperty(ref _actionHint, value);
        }

        public string TargetElementName
        {
            get => _targetElementName;
            set => SetProperty(ref _targetElementName, value);
        }

        public WalkthroughPlacement PreferredPlacement
        {
            get => _preferredPlacement;
            set => SetProperty(ref _preferredPlacement, value);
        }

        public double SpotlightPadding
        {
            get => _spotlightPadding;
            set => SetProperty(ref _spotlightPadding, value);
        }

        public double SpotlightCornerRadius
        {
            get => _spotlightCornerRadius;
            set => SetProperty(ref _spotlightCornerRadius, value);
        }
    }
}
