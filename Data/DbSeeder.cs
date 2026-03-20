using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceShiftingManagement.Data
{
    public static class DbSeeder
    {
        private static readonly string[] LegacyStaffingPositionNames =
        {
            "Chicken Expert",
            "Batch Grill",
            "POS 1",
            "POS 2",
            "POS 3",
            "GEL",
            "DT Order Taker",
            "DT Cashier",
            "DT Presenter",
            "Lobby",
            "Shift Manager"
        };

        private static readonly string[] LegacyStaffingDemoUsernames = BuildLegacyStaffingDemoUsernames();

        public static void Seed(AppDbContext context)
        {
            RemoveLegacyStaffingDemoData(context);
            EnsureBarangayPositions(context);

            var allPositions = context.Positions.ToList();

            EnsureDefaultUsers(context);
            EnsureDefaultEmployees(context, allPositions);
            EnsureHolidayCalendar(context);
            EnsureBarangayHouseholds(context);
            EnsureCashForWorkEventSamples(context);
        }

        private static string[] BuildLegacyStaffingDemoUsernames()
        {
            var usernames = new List<string> { "shiftmanager", "crew" };
            usernames.AddRange(Enumerable.Range(1, 4).Select(i => $"manager{i}"));
            usernames.AddRange(Enumerable.Range(1, 19).Select(i => $"crew{i}"));
            return usernames.ToArray();
        }

        private static void RemoveLegacyStaffingDemoData(AppDbContext context)
        {
            var hasLegacySignature =
                context.Positions.Any(position => LegacyStaffingPositionNames.Contains(position.Name)) ||
                context.Users.Any(user => LegacyStaffingDemoUsernames.Contains(user.Username));

            if (!hasLegacySignature)
            {
                return;
            }

            context.Attendances.RemoveRange(context.Attendances);
            context.ShiftAssignments.RemoveRange(context.ShiftAssignments);
            context.Shifts.RemoveRange(context.Shifts);
            context.Payrolls.RemoveRange(context.Payrolls);
            context.LeaveRequests.RemoveRange(context.LeaveRequests);
            context.RecruitmentCandidates.RemoveRange(context.RecruitmentCandidates);
            context.EmployeeExits.RemoveRange(context.EmployeeExits);
            context.PerformanceGoals.RemoveRange(context.PerformanceGoals);
            context.TrainingRecords.RemoveRange(context.TrainingRecords);
            context.EngagementSurveys.RemoveRange(context.EngagementSurveys);

            var legacyUserIds = context.Users
                .Where(user => LegacyStaffingDemoUsernames.Contains(user.Username))
                .Select(user => user.Id)
                .ToList();

            if (legacyUserIds.Count > 0)
            {
                context.Notifications.RemoveRange(
                    context.Notifications.Where(notification => legacyUserIds.Contains(notification.UserId)));
                context.ActivityLogs.RemoveRange(
                    context.ActivityLogs.Where(log => log.UserId.HasValue && legacyUserIds.Contains(log.UserId.Value)));
                context.FingerprintTemplates.RemoveRange(
                    context.FingerprintTemplates.Where(template =>
                        legacyUserIds.Contains(template.UserId) ||
                        (template.EnrolledByUserId.HasValue && legacyUserIds.Contains(template.EnrolledByUserId.Value))));
                context.UserProfiles.RemoveRange(
                    context.UserProfiles.Where(profile => legacyUserIds.Contains(profile.UserId)));
                context.UserPreferences.RemoveRange(
                    context.UserPreferences.Where(preference => legacyUserIds.Contains(preference.UserId)));

                var legacyEmployeeIds = context.Employees
                    .Where(employee => legacyUserIds.Contains(employee.UserId))
                    .Select(employee => employee.Id)
                    .ToList();

                if (legacyEmployeeIds.Count > 0)
                {
                    context.Employees.RemoveRange(
                        context.Employees.Where(employee => legacyEmployeeIds.Contains(employee.Id)));
                }

                context.Users.RemoveRange(
                    context.Users.Where(user => legacyUserIds.Contains(user.Id)));
            }

            context.SaveChanges();

            var removablePositions = context.Positions
                .Where(position => LegacyStaffingPositionNames.Contains(position.Name))
                .ToList();

            if (removablePositions.Count > 0)
            {
                context.Positions.RemoveRange(removablePositions);
                context.SaveChanges();
            }
        }

        private static void EnsureBarangayPositions(AppDbContext context)
        {
            var requiredPositions = new[]
            {
                new Position { Name = "Barangay Administrator", Area = PositionArea.Lobby },
                new Position { Name = "Treasury Operations Officer", Area = PositionArea.Lobby },
                new Position { Name = "Cash-for-Work Coordinator", Area = PositionArea.Lobby }
            };

            var existingNames = context.Positions
                .Select(position => position.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var position in requiredPositions)
            {
                if (!existingNames.Contains(position.Name))
                {
                    context.Positions.Add(position);
                }
            }

            if (context.ChangeTracker.HasChanges())
            {
                context.SaveChanges();
            }
        }

        private static void EnsureHolidayCalendar(AppDbContext context)
        {
            if (context.Holidays.Any())
            {
                return;
            }

            var holidays = new List<Holiday>
            {
                new Holiday { HolidayDate = new DateTime(2026, 1, 1), Name = "New Year's Day", IsDoublePay = true },
                new Holiday { HolidayDate = new DateTime(2026, 12, 25), Name = "Christmas Day", IsDoublePay = true },
                new Holiday { HolidayDate = new DateTime(2026, 12, 30), Name = "Rizal Day", IsDoublePay = true }
            };

            context.Holidays.AddRange(holidays);
            context.SaveChanges();
        }

        private static void EnsureDefaultUsers(AppDbContext context)
        {
            AddOrUpdateDemoUser(context, "admin", "admin@barangay.local", "admin123", UserRole.Admin);
            AddOrUpdateDemoUser(context, "hr", "treasurer@barangay.local", "treasurer123", UserRole.HRStaff);
            context.SaveChanges();
        }

        private static void AddOrUpdateDemoUser(
            AppDbContext context,
            string username,
            string email,
            string plainPassword,
            UserRole role)
        {
            var existing = context.Users.FirstOrDefault(u => u.Email == email || u.Username == username);
            if (existing == null)
            {
                context.Users.Add(new User
                {
                    Username = username,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword),
                    Role = role,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
                return;
            }

            bool hasChanges = false;

            if (existing.Username != username)
            {
                existing.Username = username;
                hasChanges = true;
            }

            if (existing.Email != email)
            {
                existing.Email = email;
                hasChanges = true;
            }

            if (existing.Role != role)
            {
                existing.Role = role;
                hasChanges = true;
            }

            if (!existing.IsActive)
            {
                existing.IsActive = true;
                hasChanges = true;
            }

            if (!BCrypt.Net.BCrypt.Verify(plainPassword, existing.PasswordHash))
            {
                existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
                hasChanges = true;
            }

            if (hasChanges)
            {
                existing.UpdatedAt = DateTime.Now;
            }
        }

        private static void EnsureDefaultEmployees(AppDbContext context, List<Position> allPositions)
        {
            if (allPositions.Count == 0)
            {
                return;
            }

            int fallbackPositionId = allPositions[0].Id;
            int adminPositionId = allPositions
                .Where(position => position.Name == "Barangay Administrator")
                .Select(position => (int?)position.Id)
                .FirstOrDefault() ?? fallbackPositionId;
            int treasuryPositionId = allPositions
                .Where(position => position.Name == "Treasury Operations Officer")
                .Select(position => (int?)position.Id)
                .FirstOrDefault() ?? fallbackPositionId;

            var adminUser = context.Users.FirstOrDefault(u => u.Role == UserRole.Admin);
            if (adminUser != null)
            {
                AddOrUpdateEmployee(
                    context,
                    adminUser.Id,
                    "Barangay System Administrator",
                    adminPositionId,
                    100.00m,
                    DateTime.Now.AddYears(-2));
            }

            var hrUsers = context.Users
                .Where(u => u.Role == UserRole.HRStaff)
                .OrderBy(u => u.Id)
                .ToList();
            for (int i = 0; i < hrUsers.Count; i++)
            {
                AddOrUpdateEmployee(
                    context,
                    hrUsers[i].Id,
                    i == 0 ? "Treasury Operations Officer" : $"Treasury Staff {i + 1}",
                    treasuryPositionId,
                    75.00m,
                    DateTime.Now.AddYears(-1));
            }

            context.SaveChanges();
        }

        private static void AddOrUpdateEmployee(
            AppDbContext context,
            int userId,
            string fullName,
            int positionId,
            decimal hourlyRate,
            DateTime dateHired)
        {
            var employee = context.Employees.FirstOrDefault(e => e.UserId == userId);
            if (employee == null)
            {
                context.Employees.Add(new Employee
                {
                    UserId = userId,
                    FullName = fullName,
                    PositionId = positionId,
                    HourlyRate = hourlyRate,
                    DateHired = dateHired,
                    Status = EmployeeStatus.Active
                });
                return;
            }

            employee.FullName = fullName;
            employee.PositionId = positionId;
            employee.HourlyRate = hourlyRate;
            employee.Status = EmployeeStatus.Active;
        }

        private static void EnsureRecruitmentCandidates(AppDbContext context)
        {
            if (context.RecruitmentCandidates.Any())
            {
                return;
            }

            var now = DateTime.Now;
            var records = new List<RecruitmentCandidate>
            {
                new RecruitmentCandidate
                {
                    FullName = "Alden Cruz",
                    Email = "alden.cruz@samplemail.com",
                    Source = RecruitmentSource.Referral,
                    Stage = RecruitmentStage.Interview,
                    AppliedAt = now.AddDays(-18),
                    InterviewedAt = now.AddDays(-10),
                    Notes = "Strong kitchen background."
                },
                new RecruitmentCandidate
                {
                    FullName = "Mia Santos",
                    Email = "mia.santos@samplemail.com",
                    Source = RecruitmentSource.JobBoard,
                    Stage = RecruitmentStage.OfferExtended,
                    AppliedAt = now.AddDays(-22),
                    InterviewedAt = now.AddDays(-14),
                    OfferedAt = now.AddDays(-3),
                    Notes = "Offer pending response."
                },
                new RecruitmentCandidate
                {
                    FullName = "Rhea Lim",
                    Email = "rhea.lim@samplemail.com",
                    Source = RecruitmentSource.Campus,
                    Stage = RecruitmentStage.Hired,
                    AppliedAt = now.AddDays(-40),
                    InterviewedAt = now.AddDays(-32),
                    OfferedAt = now.AddDays(-28),
                    HiredAt = now.AddDays(-24),
                    Notes = "Onboarding completed."
                },
                new RecruitmentCandidate
                {
                    FullName = "Jake Dizon",
                    Email = "jake.dizon@samplemail.com",
                    Source = RecruitmentSource.WalkIn,
                    Stage = RecruitmentStage.Applied,
                    AppliedAt = now.AddDays(-4),
                    Notes = "Awaiting screening."
                },
                new RecruitmentCandidate
                {
                    FullName = "Carla Torres",
                    Email = "carla.torres@samplemail.com",
                    Source = RecruitmentSource.SocialMedia,
                    Stage = RecruitmentStage.Rejected,
                    AppliedAt = now.AddDays(-16),
                    InterviewedAt = now.AddDays(-9),
                    Notes = "Did not meet minimum availability."
                }
            };

            context.RecruitmentCandidates.AddRange(records);
            context.SaveChanges();
        }

        private static void EnsureEmployeeExits(AppDbContext context)
        {
            if (context.EmployeeExits.Any())
            {
                return;
            }

            var recordedBy = context.Users
                .Where(u => u.Role == UserRole.HRStaff || u.Role == UserRole.Admin)
                .OrderBy(u => u.Id)
                .Select(u => u.Id)
                .FirstOrDefault();

            if (recordedBy == 0)
            {
                return;
            }

            var crewToExit = context.Employees
                .Include(e => e.User)
                .Where(e => e.User.Role == UserRole.Crew && e.Status == EmployeeStatus.Active)
                .OrderBy(e => e.Id)
                .Take(2)
                .ToList();

            if (crewToExit.Count == 0)
            {
                return;
            }

            var exits = new List<EmployeeExit>();
            for (int i = 0; i < crewToExit.Count; i++)
            {
                var employee = crewToExit[i];
                var isVoluntary = i == 0;
                var type = isVoluntary ? EmployeeExitType.Resignation : EmployeeExitType.EndOfContract;
                var reason = isVoluntary
                    ? "Accepted offer closer to home branch."
                    : "Contract period completed.";

                exits.Add(new EmployeeExit
                {
                    EmployeeId = employee.Id,
                    ExitType = type,
                    IsVoluntary = isVoluntary,
                    Reason = reason,
                    LastWorkingDate = DateTime.Today.AddDays(-(14 + i * 7)),
                    RecordedBy = recordedBy,
                    Notes = "Seeded Phase 2 retention sample.",
                    RecordedAt = DateTime.Now.AddDays(-(12 + i * 7))
                });

                employee.Status = EmployeeStatus.Inactive;
            }

            context.EmployeeExits.AddRange(exits);
            context.SaveChanges();
        }

        private static void EnsurePerformanceGoals(AppDbContext context)
        {
            if (context.PerformanceGoals.Any())
            {
                return;
            }

            var activeEmployees = context.Employees
                .Include(e => e.Position)
                .Where(e => e.Status == EmployeeStatus.Active)
                .OrderBy(e => e.Id)
                .Take(14)
                .ToList();

            if (activeEmployees.Count == 0)
            {
                return;
            }

            var random = new Random(11);
            var templates = new[]
            {
                "Drive-thru Service Time Improvement",
                "Order Accuracy Consistency",
                "Peak Hour Team Coordination",
                "Food Safety Checklist Compliance",
                "Guest Satisfaction Recovery"
            };

            var goals = new List<PerformanceGoal>();
            for (int i = 0; i < activeEmployees.Count; i++)
            {
                var employee = activeEmployees[i];
                var completion = random.Next(30, 101);
                var dueDate = DateTime.Today.AddDays(random.Next(-20, 45));

                var status = completion >= 100
                    ? PerformanceGoalStatus.Completed
                    : dueDate < DateTime.Today
                        ? PerformanceGoalStatus.Overdue
                        : completion < 40
                            ? PerformanceGoalStatus.NotStarted
                            : PerformanceGoalStatus.InProgress;

                goals.Add(new PerformanceGoal
                {
                    EmployeeId = employee.Id,
                    GoalTitle = templates[i % templates.Length],
                    CompletionPercent = completion,
                    ReviewScore = Math.Round((decimal)(random.NextDouble() * 2.0 + 3.0), 2),
                    ManagerFeedbackScore = Math.Round((decimal)(random.NextDouble() * 2.0 + 3.0), 2),
                    DueDate = dueDate,
                    Status = status,
                    CreatedAt = DateTime.Now.AddDays(-random.Next(15, 90)),
                    UpdatedAt = DateTime.Now.AddDays(-random.Next(1, 14))
                });
            }

            context.PerformanceGoals.AddRange(goals);
            context.SaveChanges();
        }

        private static void EnsureTrainingRecords(AppDbContext context)
        {
            if (context.TrainingRecords.Any())
            {
                return;
            }

            var activeEmployees = context.Employees
                .Include(e => e.Position)
                .Where(e => e.Status == EmployeeStatus.Active)
                .OrderBy(e => e.Id)
                .Take(16)
                .ToList();

            if (activeEmployees.Count == 0)
            {
                return;
            }

            var random = new Random(17);
            var trainings = new[]
            {
                "Food Safety Refresher",
                "Customer Complaint Handling",
                "POS Error Recovery",
                "Shift Handover Discipline",
                "Speed of Service Coaching"
            };

            var records = new List<TrainingRecord>();
            for (int i = 0; i < activeEmployees.Count; i++)
            {
                var employee = activeEmployees[i];
                var assignedAt = DateTime.Today.AddDays(-random.Next(10, 70));
                var dueDate = assignedAt.AddDays(random.Next(7, 30));
                var isCompleted = random.NextDouble() > 0.35;
                var completedAt = isCompleted ? dueDate.AddDays(-random.Next(0, 5)) : (DateTime?)null;
                var status = isCompleted
                    ? TrainingStatus.Completed
                    : dueDate < DateTime.Today
                        ? TrainingStatus.Overdue
                        : TrainingStatus.Pending;

                records.Add(new TrainingRecord
                {
                    EmployeeId = employee.Id,
                    TrainingName = trainings[i % trainings.Length],
                    IsMandatory = i % 3 != 0,
                    AssignedAt = assignedAt,
                    DueDate = dueDate,
                    CompletedAt = completedAt,
                    EffectivenessScore = isCompleted
                        ? Math.Round((decimal)(random.NextDouble() * 2.0 + 3.0), 2)
                        : null,
                    Status = status,
                    CreatedAt = assignedAt
                });
            }

            context.TrainingRecords.AddRange(records);
            context.SaveChanges();
        }

        private static void EnsureEngagementSurveys(AppDbContext context)
        {
            if (context.EngagementSurveys.Any())
            {
                return;
            }

            var activeEmployees = context.Employees
                .Where(e => e.Status == EmployeeStatus.Active)
                .OrderBy(e => e.Id)
                .Take(18)
                .ToList();

            if (activeEmployees.Count == 0)
            {
                return;
            }

            var random = new Random(23);
            var surveys = new List<EngagementSurvey>();
            for (int i = 0; i < activeEmployees.Count; i++)
            {
                var employee = activeEmployees[i];
                var surveyDate = DateTime.Today.AddDays(-random.Next(0, 45));
                var engagement = Math.Round((decimal)(random.NextDouble() * 45.0 + 50.0), 2);
                var wellbeing = Math.Round((decimal)(random.NextDouble() * 45.0 + 50.0), 2);
                var enps = random.Next(-40, 71);
                var burnoutRisk = wellbeing < 60m
                    ? BurnoutRiskLevel.High
                    : wellbeing < 75m
                        ? BurnoutRiskLevel.Medium
                        : BurnoutRiskLevel.Low;

                surveys.Add(new EngagementSurvey
                {
                    EmployeeId = employee.Id,
                    SurveyDate = surveyDate,
                    EnpsScore = enps,
                    EngagementScore = engagement,
                    WellbeingScore = wellbeing,
                    BurnoutRisk = burnoutRisk,
                    Comments = burnoutRisk == BurnoutRiskLevel.High
                        ? "Needs schedule balance and manager follow-up."
                        : null,
                    CreatedAt = surveyDate.AddHours(10)
                });
            }

            context.EngagementSurveys.AddRange(surveys);
            context.SaveChanges();
        }

        private static void EnsureLeaveRequests(AppDbContext context)
        {
            if (context.LeaveRequests.Any())
            {
                return;
            }

            var random = new Random(29);
            var approverId = context.Users
                .Where(u => u.Role == UserRole.HRStaff || u.Role == UserRole.Admin)
                .OrderBy(u => u.Id)
                .Select(u => u.Id)
                .FirstOrDefault();

            var targetEmployees = context.Employees
                .AsNoTracking()
                .Where(e => e.Status == EmployeeStatus.Active)
                .OrderBy(e => e.Id)
                .Take(10)
                .ToList();

            if (targetEmployees.Count == 0)
            {
                return;
            }

            var records = new List<LeaveRequest>();
            for (int i = 0; i < targetEmployees.Count; i++)
            {
                var employee = targetEmployees[i];
                var startDate = DateTime.Today.AddDays(random.Next(-10, 15));
                var duration = random.Next(1, 4);
                var status = i % 4 == 0
                    ? LeaveStatus.Pending
                    : i % 5 == 0
                        ? LeaveStatus.Rejected
                        : LeaveStatus.Approved;

                records.Add(new LeaveRequest
                {
                    EmployeeId = employee.Id,
                    Type = (LeaveType)(i % Enum.GetValues(typeof(LeaveType)).Length),
                    StartDate = startDate,
                    EndDate = startDate.AddDays(duration - 1),
                    Reason = status == LeaveStatus.Rejected
                        ? "Personal errand conflict with shift peak."
                        : "Scheduled personal leave.",
                    Status = status,
                    ApprovedBy = status == LeaveStatus.Pending ? null : (approverId == 0 ? null : approverId),
                    ApprovedAt = status == LeaveStatus.Pending ? null : DateTime.Now.AddDays(-random.Next(1, 5)),
                    RejectionReason = status == LeaveStatus.Rejected ? "Insufficient staffing for requested date." : null,
                    CreatedAt = DateTime.Now.AddDays(-random.Next(2, 20))
                });
            }

            context.LeaveRequests.AddRange(records);
            context.SaveChanges();
        }

        private static void EnsureShiftScheduleAndAssignments(AppDbContext context, List<Position> allPositions)
        {
            if (context.Shifts.Any() || allPositions.Count == 0)
            {
                return;
            }

            int createdBy = context.Users
                .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.HRStaff)
                .OrderBy(u => u.Id)
                .Select(u => u.Id)
                .FirstOrDefault();

            if (createdBy == 0)
            {
                return;
            }

            var activeEmployees = context.Employees
                .Include(e => e.Position)
                .Where(e => e.Status == EmployeeStatus.Active)
                .OrderBy(e => e.Id)
                .ToList();

            if (activeEmployees.Count == 0)
            {
                return;
            }

            var areaPositions = allPositions
                .GroupBy(p => p.Area)
                .ToDictionary(g => g.Key, g => g.First());

            var shiftTemplates = new[]
            {
                (Area: PositionArea.Kitchen, Start: new TimeSpan(6, 0, 0), End: new TimeSpan(14, 0, 0), Required: 3),
                (Area: PositionArea.POS, Start: new TimeSpan(8, 0, 0), End: new TimeSpan(16, 0, 0), Required: 3),
                (Area: PositionArea.DT, Start: new TimeSpan(10, 0, 0), End: new TimeSpan(18, 0, 0), Required: 3),
                (Area: PositionArea.Lobby, Start: new TimeSpan(12, 0, 0), End: new TimeSpan(20, 0, 0), Required: 2)
            };

            var shifts = new List<Shift>();
            var assignments = new List<ShiftAssignment>();
            var dayStart = DateTime.Today.AddDays(-6);
            var dayEnd = DateTime.Today.AddDays(7);

            foreach (var day in Enumerable.Range(0, (dayEnd - dayStart).Days + 1).Select(offset => dayStart.AddDays(offset)))
            {
                foreach (var template in shiftTemplates)
                {
                    if (!areaPositions.TryGetValue(template.Area, out var position))
                    {
                        continue;
                    }

                    var shift = new Shift
                    {
                        ShiftDate = day.Date,
                        StartTime = template.Start,
                        EndTime = template.End,
                        PositionId = position.Id,
                        CreatedBy = createdBy,
                        CreatedAt = DateTime.Now.AddDays(-1)
                    };
                    shifts.Add(shift);
                }
            }

            context.Shifts.AddRange(shifts);
            context.SaveChanges();

            var employeesByArea = activeEmployees
                .GroupBy(e => e.Position.Area)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Id).ToList());

            int cursor = 0;
            foreach (var shift in shifts)
            {
                var area = allPositions.First(p => p.Id == shift.PositionId).Area;
                if (!employeesByArea.TryGetValue(area, out var employeeIds) || employeeIds.Count == 0)
                {
                    continue;
                }

                int requiredCount = area == PositionArea.Lobby ? 2 : 3;
                for (int i = 0; i < requiredCount; i++)
                {
                    var employeeId = employeeIds[(cursor + i) % employeeIds.Count];
                    assignments.Add(new ShiftAssignment
                    {
                        ShiftId = shift.Id,
                        EmployeeId = employeeId
                    });
                }

                cursor++;
            }

            context.ShiftAssignments.AddRange(assignments);
            context.SaveChanges();
        }

        private static void EnsureAttendanceSamples(AppDbContext context)
        {
            if (context.Attendances.Any())
            {
                return;
            }

            var random = new Random(31);
            var assignments = context.ShiftAssignments
                .Include(sa => sa.Shift)
                .Where(sa => sa.Shift.ShiftDate.Date <= DateTime.Today)
                .ToList();

            if (assignments.Count == 0)
            {
                return;
            }

            var records = new List<Attendance>();
            foreach (var assignment in assignments)
            {
                var shiftDate = assignment.Shift.ShiftDate.Date;
                var start = shiftDate.Add(assignment.Shift.StartTime);
                var end = shiftDate.Add(assignment.Shift.EndTime);
                if (end <= start)
                {
                    end = end.AddDays(1);
                }

                bool shouldBeAbsent = random.NextDouble() < 0.08;
                if (shouldBeAbsent && shiftDate < DateTime.Today)
                {
                    continue;
                }

                var lateMinutes = random.Next(0, 18);
                var earlyOutMinutes = random.Next(0, 30);
                var overtimeMinutes = random.NextDouble() < 0.18 ? random.Next(15, 75) : 0;

                var timeIn = start.AddMinutes(lateMinutes);
                var timeOut = shiftDate == DateTime.Today && random.NextDouble() < 0.15
                    ? (DateTime?)null
                    : end.AddMinutes(overtimeMinutes - earlyOutMinutes);

                var totalHours = timeOut.HasValue
                    ? Math.Max(0m, (decimal)(timeOut.Value - timeIn).TotalHours)
                    : 0m;

                var scheduledHours = Math.Max(0m, (decimal)(end - start).TotalHours);
                var overtimeHours = timeOut.HasValue
                    ? Math.Max(0m, totalHours - scheduledHours)
                    : 0m;

                records.Add(new Attendance
                {
                    EmployeeId = assignment.EmployeeId,
                    ShiftId = assignment.ShiftId,
                    TimeIn = timeIn,
                    TimeOut = timeOut,
                    TotalHours = totalHours,
                    OvertimeHours = overtimeHours,
                    Status = timeOut.HasValue ? AttendanceStatus.Closed : AttendanceStatus.Open
                });
            }

            context.Attendances.AddRange(records);
            context.SaveChanges();
        }

        private static void EnsurePayrollSamples(AppDbContext context)
        {
            if (context.Payrolls.Any())
            {
                return;
            }

            var generatedBy = context.Users
                .Where(u => u.Role == UserRole.HRStaff || u.Role == UserRole.Admin)
                .OrderBy(u => u.Id)
                .Select(u => u.Id)
                .FirstOrDefault();

            if (generatedBy == 0)
            {
                return;
            }

            var employees = context.Employees
                .Where(e => e.Status == EmployeeStatus.Active)
                .OrderBy(e => e.Id)
                .Take(14)
                .ToList();

            if (employees.Count == 0)
            {
                return;
            }

            var random = new Random(37);
            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-3);
            var payrolls = new List<Payroll>();

            for (int monthOffset = 0; monthOffset < 4; monthOffset++)
            {
                var start = monthStart.AddMonths(monthOffset);
                var end = start.AddMonths(1).AddDays(-1);

                foreach (var employee in employees)
                {
                    var regularPay = Math.Round(employee.HourlyRate * random.Next(130, 175), 2);
                    var overtimePay = Math.Round(employee.HourlyRate * 1.25m * random.Next(6, 24), 2);
                    var holidayPay = Math.Round(employee.HourlyRate * 2.0m * random.Next(0, 8), 2);

                    payrolls.Add(new Payroll
                    {
                        EmployeeId = employee.Id,
                        PeriodStart = start,
                        PeriodEnd = end,
                        RegularPay = regularPay,
                        OvertimePay = overtimePay,
                        HolidayPay = holidayPay,
                        TotalPay = regularPay + overtimePay + holidayPay,
                        GeneratedAt = end.AddDays(1).AddHours(9),
                        GeneratedBy = generatedBy
                    });
                }
            }

            context.Payrolls.AddRange(payrolls);
            context.SaveChanges();
        }

        private static void EnsureBarangayHouseholds(AppDbContext context)
        {
            if (context.Households.Any())
            {
                return;
            }

            var households = new List<Household>
            {
                new()
                {
                    HouseholdCode = "HH-0001",
                    HeadName = "Juan A. Dela Cruz",
                    AddressLine = "Sitio Riverside, Barangay Sample",
                    Purok = "Purok 1",
                    ContactNumber = "09171234567",
                    Status = HouseholdStatus.Active
                },
                new()
                {
                    HouseholdCode = "HH-0002",
                    HeadName = "Maria B. Santos",
                    AddressLine = "Purok Malinawon, Barangay Sample",
                    Purok = "Purok 2",
                    ContactNumber = "09181234567",
                    Status = HouseholdStatus.Active
                },
                new()
                {
                    HouseholdCode = "HH-0003",
                    HeadName = "Pedro R. Mendoza",
                    AddressLine = "Zone 3, Barangay Sample",
                    Purok = "Purok 3",
                    ContactNumber = "09191234567",
                    Status = HouseholdStatus.Active
                }
            };

            context.Households.AddRange(households);
            context.SaveChanges();

            var members = new List<HouseholdMember>
            {
                new()
                {
                    HouseholdId = households[0].Id,
                    FullName = "Juan A. Dela Cruz",
                    RelationshipToHead = "Head",
                    Occupation = "Tricycle Driver",
                    IsCashForWorkEligible = true
                },
                new()
                {
                    HouseholdId = households[0].Id,
                    FullName = "Josefina Dela Cruz",
                    RelationshipToHead = "Spouse",
                    Occupation = "Vendor",
                    IsCashForWorkEligible = false
                },
                new()
                {
                    HouseholdId = households[1].Id,
                    FullName = "Maria B. Santos",
                    RelationshipToHead = "Head",
                    Occupation = "Laundry Worker",
                    IsCashForWorkEligible = true
                },
                new()
                {
                    HouseholdId = households[1].Id,
                    FullName = "Rodel C. Santos",
                    RelationshipToHead = "Son",
                    Occupation = "Street Sweeper",
                    IsCashForWorkEligible = true
                },
                new()
                {
                    HouseholdId = households[2].Id,
                    FullName = "Pedro R. Mendoza",
                    RelationshipToHead = "Head",
                    Occupation = "Construction Helper",
                    IsCashForWorkEligible = true
                },
                new()
                {
                    HouseholdId = households[2].Id,
                    FullName = "Charito F. Mendoza",
                    RelationshipToHead = "Spouse",
                    Occupation = "Home-based Worker",
                    IsCashForWorkEligible = true
                }
            };

            context.HouseholdMembers.AddRange(members);
            context.SaveChanges();
        }

        private static void EnsureCashForWorkEventSamples(AppDbContext context)
        {
            if (context.CashForWorkEvents.Any())
            {
                return;
            }

            var createdByUserId = context.Users
                .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.HRStaff)
                .OrderBy(u => u.Id)
                .Select(u => u.Id)
                .FirstOrDefault();

            if (createdByUserId == 0)
            {
                return;
            }

            var cashForWorkEvent = new CashForWorkEvent
            {
                Title = "Canal Clearing Cash-for-Work",
                Location = "Purok 2 Riverside",
                EventDate = DateTime.Today,
                StartTime = new TimeSpan(7, 0, 0),
                EndTime = new TimeSpan(12, 0, 0),
                Notes = "Bring cleaning tools and attendance logbook.",
                CreatedByUserId = createdByUserId,
                Status = CashForWorkEventStatus.Open
            };

            context.CashForWorkEvents.Add(cashForWorkEvent);
            context.SaveChanges();

            var participantMembers = context.HouseholdMembers
                .Where(m => m.IsCashForWorkEligible)
                .OrderBy(m => m.Id)
                .Take(4)
                .ToList();

            if (participantMembers.Count == 0)
            {
                return;
            }

            var participants = participantMembers
                .Select(member => new CashForWorkParticipant
                {
                    EventId = cashForWorkEvent.Id,
                    HouseholdMemberId = member.Id,
                    AddedByUserId = createdByUserId,
                    AddedAt = DateTime.Now.AddMinutes(-30)
                })
                .ToList();

            context.CashForWorkParticipants.AddRange(participants);
            context.SaveChanges();
        }
    }
}
