using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace AttendanceShiftingManagement.ViewModels
{
    public sealed class HouseholdRegistryViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly ObservableCollection<HouseholdRegistryItem> _households;
        private readonly ObservableCollection<HouseholdMember> _selectedMembers;
        private ICollectionView _householdsView;
        private HouseholdRegistryItem? _selectedHousehold;
        private string _searchText = string.Empty;
        private int _totalHouseholds;
        private int _totalMembers;
        private int _cashForWorkEligibleCount;

        public HouseholdRegistryViewModel()
        {
            _context = new AppDbContext();
            _households = new ObservableCollection<HouseholdRegistryItem>();
            _selectedMembers = new ObservableCollection<HouseholdMember>();
            _householdsView = CollectionViewSource.GetDefaultView(_households);
            LoadData();
        }

        public ICollectionView HouseholdsView
        {
            get => _householdsView;
            private set => SetProperty(ref _householdsView, value);
        }

        public ObservableCollection<HouseholdMember> SelectedMembers => _selectedMembers;

        public HouseholdRegistryItem? SelectedHousehold
        {
            get => _selectedHousehold;
            set
            {
                if (SetProperty(ref _selectedHousehold, value))
                {
                    LoadSelectedMembers();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilter();
                }
            }
        }

        public int TotalHouseholds
        {
            get => _totalHouseholds;
            set => SetProperty(ref _totalHouseholds, value);
        }

        public int TotalMembers
        {
            get => _totalMembers;
            set => SetProperty(ref _totalMembers, value);
        }

        public int CashForWorkEligibleCount
        {
            get => _cashForWorkEligibleCount;
            set => SetProperty(ref _cashForWorkEligibleCount, value);
        }

        private void LoadData()
        {
            _context.ChangeTracker.Clear();

            var households = _context.Households
                .AsNoTracking()
                .Include(h => h.Members)
                .OrderBy(h => h.HouseholdCode)
                .ToList();

            _households.Clear();
            foreach (var household in households)
            {
                _households.Add(new HouseholdRegistryItem
                {
                    Id = household.Id,
                    HouseholdCode = household.HouseholdCode,
                    HeadName = household.HeadName,
                    AddressLine = household.AddressLine,
                    Purok = household.Purok,
                    ContactNumber = household.ContactNumber,
                    Status = household.Status,
                    MemberCount = household.Members.Count,
                    EligibleMemberCount = household.Members.Count(member => member.IsCashForWorkEligible)
                });
            }

            HouseholdsView = CollectionViewSource.GetDefaultView(_households);
            ApplyFilter();

            TotalHouseholds = households.Count;
            TotalMembers = households.Sum(h => h.Members.Count);
            CashForWorkEligibleCount = households.Sum(h => h.Members.Count(member => member.IsCashForWorkEligible));

            SelectedHousehold = _households.FirstOrDefault();
        }

        private void ApplyFilter()
        {
            HouseholdsView.Filter = item =>
            {
                if (item is not HouseholdRegistryItem household)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    return true;
                }

                return household.HouseholdCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || household.HeadName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || household.AddressLine.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || household.Purok.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            };

            HouseholdsView.Refresh();
        }

        private void LoadSelectedMembers()
        {
            _selectedMembers.Clear();
            if (SelectedHousehold == null)
            {
                return;
            }

            var members = _context.HouseholdMembers
                .AsNoTracking()
                .Where(member => member.HouseholdId == SelectedHousehold.Id)
                .OrderBy(member => member.FullName)
                .ToList();

            foreach (var member in members)
            {
                _selectedMembers.Add(member);
            }
        }
    }

    public sealed class HouseholdRegistryItem
    {
        public int Id { get; set; }
        public string HouseholdCode { get; set; } = string.Empty;
        public string HeadName { get; set; } = string.Empty;
        public string AddressLine { get; set; } = string.Empty;
        public string Purok { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public HouseholdStatus Status { get; set; }
        public int MemberCount { get; set; }
        public int EligibleMemberCount { get; set; }
    }
}
