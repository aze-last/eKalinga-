using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace AttendanceShiftingManagement.Services
{
    public class PositionService
    {
        private readonly AppDbContext _context;

        public PositionService(AppDbContext context)
        {
            _context = context;
        }

        public List<Position> GetAllPositions()
        {
            return _context.Positions.ToList();
        }

        public void AddPosition(Position position)
        {
            _context.Positions.Add(position);
            _context.SaveChanges();
        }

        public void UpdatePosition(Position position)
        {
            var existing = _context.Positions.Find(position.Id);
            if (existing != null)
            {
                existing.Name = position.Name;
                existing.Area = position.Area;
                _context.SaveChanges();
            }
        }

        public void DeletePosition(int id)
        {
            var pos = _context.Positions.Find(id);
            if (pos != null)
            {
                _context.Positions.Remove(pos);
                _context.SaveChanges();
            }
        }
    }
}
