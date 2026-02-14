-- ASMS MySQL Schema (aligned with current WPF + EF Core project)
-- MySQL 8+

CREATE DATABASE IF NOT EXISTS attendance_shifting_db;
USE attendance_shifting_db;

CREATE TABLE IF NOT EXISTS users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) NOT NULL,
    email VARCHAR(100) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(40) NOT NULL, -- Admin, HRStaff, Manager, Crew
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    UNIQUE KEY uq_users_username (username),
    UNIQUE KEY uq_users_email (email)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS positions (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    area VARCHAR(40) NOT NULL -- Kitchen, POS, DT, Lobby
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS employees (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    full_name VARCHAR(150) NOT NULL,
    position_id INT NOT NULL,
    hourly_rate DECIMAL(10,2) NOT NULL,
    date_hired DATETIME(6) NOT NULL,
    status VARCHAR(40) NOT NULL DEFAULT 'Active',
    CONSTRAINT fk_employees_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_employees_position FOREIGN KEY (position_id) REFERENCES positions(id) ON DELETE CASCADE,
    UNIQUE KEY uq_employees_user_id (user_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS shifts (
    id INT AUTO_INCREMENT PRIMARY KEY,
    shift_date DATETIME(6) NOT NULL,
    start_time TIME(6) NOT NULL,
    end_time TIME(6) NOT NULL,
    position_id INT NOT NULL,
    created_by INT NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_shifts_position FOREIGN KEY (position_id) REFERENCES positions(id) ON DELETE CASCADE,
    CONSTRAINT fk_shifts_created_by FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS shift_assignments (
    id INT AUTO_INCREMENT PRIMARY KEY,
    shift_id INT NOT NULL,
    employee_id INT NOT NULL,
    CONSTRAINT fk_shift_assignments_shift FOREIGN KEY (shift_id) REFERENCES shifts(id) ON DELETE CASCADE,
    CONSTRAINT fk_shift_assignments_employee FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE,
    UNIQUE KEY uq_shift_assignments_shift_employee (shift_id, employee_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS attendance (
    id INT AUTO_INCREMENT PRIMARY KEY,
    employee_id INT NOT NULL,
    shift_id INT NOT NULL,
    time_in DATETIME(6) NULL,
    time_out DATETIME(6) NULL,
    total_hours DECIMAL(10,2) NOT NULL DEFAULT 0,
    overtime_hours DECIMAL(10,2) NOT NULL DEFAULT 0,
    status VARCHAR(40) NOT NULL DEFAULT 'Open',
    CONSTRAINT fk_attendance_employee FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE,
    CONSTRAINT fk_attendance_shift FOREIGN KEY (shift_id) REFERENCES shifts(id) ON DELETE CASCADE,
    INDEX ix_attendance_time_in (time_in)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS holidays (
    id INT AUTO_INCREMENT PRIMARY KEY,
    holiday_date DATETIME(6) NOT NULL,
    name VARCHAR(100) NOT NULL,
    is_double_pay TINYINT(1) NOT NULL DEFAULT 1
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS leave_balances (
    id INT AUTO_INCREMENT PRIMARY KEY,
    employee_id INT NOT NULL,
    year INT NOT NULL,
    vacation_days DECIMAL(10,2) NOT NULL DEFAULT 15,
    sick_days DECIMAL(10,2) NOT NULL DEFAULT 10,
    used_vacation_days DECIMAL(10,2) NOT NULL DEFAULT 0,
    used_sick_days DECIMAL(10,2) NOT NULL DEFAULT 0,
    CONSTRAINT fk_leave_balances_employee FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE,
    UNIQUE KEY uq_leave_balance_employee_year (employee_id, year)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS leave_requests (
    id INT AUTO_INCREMENT PRIMARY KEY,
    employee_id INT NOT NULL,
    leave_type VARCHAR(40) NOT NULL,
    start_date DATETIME(6) NOT NULL,
    end_date DATETIME(6) NOT NULL,
    reason VARCHAR(500) NOT NULL,
    status VARCHAR(40) NOT NULL DEFAULT 'Pending',
    approved_by INT NULL,
    approved_at DATETIME(6) NULL,
    rejection_reason VARCHAR(500) NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_leave_requests_employee FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE,
    CONSTRAINT fk_leave_requests_approved_by FOREIGN KEY (approved_by) REFERENCES users(id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS payroll (
    id INT AUTO_INCREMENT PRIMARY KEY,
    employee_id INT NOT NULL,
    period_start DATETIME(6) NOT NULL,
    period_end DATETIME(6) NOT NULL,
    regular_pay DECIMAL(10,2) NOT NULL DEFAULT 0,
    overtime_pay DECIMAL(10,2) NOT NULL DEFAULT 0,
    holiday_pay DECIMAL(10,2) NOT NULL DEFAULT 0,
    total_pay DECIMAL(10,2) NOT NULL DEFAULT 0,
    generated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    generated_by INT NOT NULL,
    CONSTRAINT fk_payroll_employee FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE,
    CONSTRAINT fk_payroll_generated_by FOREIGN KEY (generated_by) REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS notifications (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    type VARCHAR(40) NOT NULL,
    title VARCHAR(200) NOT NULL,
    message VARCHAR(1000) NOT NULL,
    is_read TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    action_url VARCHAR(500) NULL,
    CONSTRAINT fk_notifications_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX ix_notifications_user_created (user_id, created_at)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS activity_logs (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NULL,
    action VARCHAR(100) NOT NULL,
    entity VARCHAR(100) NOT NULL,
    entity_id INT NULL,
    details VARCHAR(1000) NOT NULL,
    ip_address VARCHAR(50) NOT NULL,
    timestamp DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_activity_logs_user FOREIGN KEY (user_id) REFERENCES users(id),
    INDEX ix_activity_logs_timestamp (timestamp)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS user_profiles (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    full_name VARCHAR(150) NOT NULL DEFAULT '',
    nickname VARCHAR(80) NOT NULL DEFAULT '',
    phone VARCHAR(30) NOT NULL DEFAULT '',
    address VARCHAR(255) NOT NULL DEFAULT '',
    emergency_contact_name VARCHAR(120) NOT NULL DEFAULT '',
    emergency_contact_phone VARCHAR(30) NOT NULL DEFAULT '',
    photo_path VARCHAR(255) NOT NULL DEFAULT '',
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_user_profiles_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    UNIQUE KEY uq_user_profiles_user (user_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS user_preferences (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    preferred_positions VARCHAR(255) NOT NULL DEFAULT '',
    preferred_days_off VARCHAR(60) NOT NULL DEFAULT '',
    preferred_shift_block VARCHAR(20) NOT NULL DEFAULT '',
    notification_types VARCHAR(80) NOT NULL DEFAULT 'Leave,Shift,Announcement',
    notification_channels VARCHAR(40) NOT NULL DEFAULT 'InApp',
    auto_notify_on_approval TINYINT(1) NOT NULL DEFAULT 1,
    report_format VARCHAR(20) NOT NULL DEFAULT 'CSV',
    default_view VARCHAR(40) NOT NULL DEFAULT 'Dashboard',
    approval_signature VARCHAR(120) NOT NULL DEFAULT '',
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_user_preferences_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    UNIQUE KEY uq_user_preferences_user (user_id)
) ENGINE=InnoDB;
