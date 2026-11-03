export type NotificationType = 'OPENING_HOURS' | 'UPDATE' | 'PROMO'

export interface SystemNotification {
  id: string
  type: NotificationType
  title: string
  content: string
  time: string
  action?: {
    label: string
    tab: 'home' | 'services' | 'pricing' | 'booking' | 'tracking'
  }
}

export const SYSTEM_NOTIFICATIONS: SystemNotification[] = [
  {
    id: 'notif-hours-01',
    type: 'OPENING_HOURS',
    title: 'Xưởng kỹ thuật đang mở cửa tiếp nhận máy',
    content: 'Chi nhánh TechCare hoạt động liên tục từ 8:00 đến 21:00 hàng ngày. Đội ngũ kỹ sư túc trực kiểm tra trực tiếp và lấy liền.',
    time: 'Hôm nay · 8:00 – 21:00',
    action: {
      label: 'Đặt lịch kiểm tra ngay',
      tab: 'booking'
    }
  },
  {
    id: 'notif-update-02',
    type: 'UPDATE',
    title: 'Nâng cấp tính năng Quét Cấu Hình 1-Click v2.4',
    content: 'Hỗ trợ tự động nhận diện và đọc thông số cho vi xử lý Intel Core Ultra, AMD Ryzen 8000 series và toàn bộ dòng máy Apple Silicon.',
    time: 'Bản cập nhật mới nhất',
    action: {
      label: 'Dùng thử quét máy',
      tab: 'booking'
    }
  },
  {
    id: 'notif-promo-03',
    type: 'PROMO',
    title: 'Miễn phí kiểm tra chuyên sâu & Tra keo tản nhiệt',
    content: 'Tặng gói vệ sinh công nghiệp cao cấp và keo gốm tản nhiệt cho mọi khách hàng đặt lịch hẹn sửa chữa trực tuyến trong tuần.',
    time: 'Áp dụng cho khách đặt online',
    action: {
      label: 'Nhận ưu đãi đặt lịch',
      tab: 'booking'
    }
  }
]

