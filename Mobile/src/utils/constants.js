/**
 * Enum Constants
 * Mirror of backend enums for consistent data handling
 */

export const UserRole = {
    Admin: 0,
    Vendor: 1,
    Member: 2,
};

export const UserRoleLabels = {
    [UserRole.Admin]: 'Admin',
    [UserRole.Vendor]: 'Vendor',
    [UserRole.Member]: 'Member',
};

export const BookingStatus = {
    Pending: 0,
    Confirmed: 1,
    InProgress: 2,
    Completed: 3,
    Cancelled: 4,
    Expired: 5,
    AwaitingPayment: 6,
    Rejected: 7,
};

export const BookingStatusLabels = {
    [BookingStatus.Pending]: 'Pending',
    [BookingStatus.Confirmed]: 'Confirmed',
    [BookingStatus.InProgress]: 'In Progress',
    [BookingStatus.Completed]: 'Completed',
    [BookingStatus.Cancelled]: 'Cancelled',
    [BookingStatus.Expired]: 'Expired',
    [BookingStatus.AwaitingPayment]: 'Awaiting Payment',
    [BookingStatus.Rejected]: 'Rejected',
};

export const PaymentStatus = {
    Pending: 0,
    Completed: 1,
    Failed: 2,
    Refunded: 3,
    PartialRefund: 4,
};

export const PaymentStatusLabels = {
    [PaymentStatus.Pending]: 'Pending',
    [PaymentStatus.Completed]: 'Completed',
    [PaymentStatus.Failed]: 'Failed',
    [PaymentStatus.Refunded]: 'Refunded',
    [PaymentStatus.PartialRefund]: 'Partial Refund',
};

export const PricingType = {
    Hourly: 0,
    Daily: 1,
    Weekly: 2,
    Monthly: 3,
};

export const PricingTypeLabels = {
    [PricingType.Hourly]: 'Hourly',
    [PricingType.Daily]: 'Daily',
    [PricingType.Weekly]: 'Weekly',
    [PricingType.Monthly]: 'Monthly',
};

export const ParkingType = {
    Open: 0,
    Covered: 1,
    Garage: 2,
    Street: 3,
    Underground: 4,
};

export const ParkingTypeLabels = {
    [ParkingType.Open]: 'Open',
    [ParkingType.Covered]: 'Covered',
    [ParkingType.Garage]: 'Garage',
    [ParkingType.Street]: 'Street',
    [ParkingType.Underground]: 'Underground',
};

export const VehicleType = {
    Car: 0,
    Motorcycle: 1,
    SUV: 2,
    Truck: 3,
    Van: 4,
    Electric: 5,
};

export const VehicleTypeLabels = {
    [VehicleType.Car]: 'Car',
    [VehicleType.Motorcycle]: 'Motorcycle',
    [VehicleType.SUV]: 'SUV',
    [VehicleType.Truck]: 'Truck',
    [VehicleType.Van]: 'Van',
    [VehicleType.Electric]: 'Electric',
};

export const PaymentMethod = {
    CreditCard: 0,
    DebitCard: 1,
    UPI: 2,
    NetBanking: 3,
    Wallet: 4,
    Cash: 5,
};

export const PaymentMethodLabels = {
    [PaymentMethod.CreditCard]: 'Credit Card',
    [PaymentMethod.DebitCard]: 'Debit Card',
    [PaymentMethod.UPI]: 'UPI',
    [PaymentMethod.NetBanking]: 'Net Banking',
    [PaymentMethod.Wallet]: 'Wallet',
    [PaymentMethod.Cash]: 'Cash',
};

export const AMENITIES = [
    'CCTV',
    'Security Guard',
    'EV Charging',
    'Covered Parking',
    'Wheelchair Accessible',
    'Restroom',
    'Lighting',
    'Valet',
    'Car Wash',
    'Air Pump',
];
