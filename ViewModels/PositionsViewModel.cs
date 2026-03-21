using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class PositionsViewModel : ObservableObject
    {
        private readonly PositionService _service;
        private string _newName = string.Empty;
        private PositionArea _newArea = PositionArea.Lobby;

        public string NewName
        {
            get => _newName;
            set => SetProperty(ref _newName, value);
        }

        public PositionArea NewArea
        {
            get => _newArea;
            set => SetProperty(ref _newArea, value);
        }

        public Array Areas => Enum.GetValues(typeof(PositionArea));

        public ObservableCollection<Position> Positions { get; } = new();

        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }

        public PositionsViewModel()
        {
            _service = new PositionService(new Data.AppDbContext());
            SaveCommand = new RelayCommand(_ => ExecuteSave(), _ => !string.IsNullOrWhiteSpace(NewName));
            DeleteCommand = new RelayCommand(p => ExecuteDelete(p as Position));

            LoadData();
        }

        private void LoadData()
        {
            Positions.Clear();
            foreach (var p in _service.GetAllPositions())
            {
                Positions.Add(p);
            }
        }

        private void ExecuteSave()
        {
            var pos = new Position { Name = NewName.Trim(), Area = NewArea };
            _service.AddPosition(pos);
            NewName = string.Empty;
            LoadData();
        }

        private void ExecuteDelete(Position? pos)
        {
            if (pos != null)
            {
                _service.DeletePosition(pos.Id);
                LoadData();
            }
        }
    }
}
