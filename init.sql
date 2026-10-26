-- 1. สร้าง anonymous role สำหรับ PostgREST
CREATE ROLE web_anon NOLOGIN;

-- 2. ให้สิทธิ์ web_anon ใช้งาน schema public
GRANT USAGE ON SCHEMA public TO web_anon;

-- 3. ตัวอย่างการสร้างตาราง products สำหรับทดสอบ
CREATE TABLE IF NOT EXISTS public.products (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    sku VARCHAR(50) UNIQUE NOT NULL,
    quantity INT DEFAULT 0,
    price NUMERIC(10, 2) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 4. กำหนดสิทธิ์ให้ web_anon เข้าถึงตารางใน public
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO web_anon;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO web_anon;

-- กำหนดสิทธิ์ล่วงหน้าสำหรับตารางที่จะสร้างใหม่ในอนาคต
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO web_anon;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO web_anon;

-- 5. ข้อมูลตัวอย่าง
INSERT INTO public.products (name, sku, quantity, price) VALUES
('Keyboard Mechanical RGB', 'KB-001', 15, 1890.00),
('Wireless Gaming Mouse', 'MS-002', 30, 990.00),
('Monitor IPS 27-inch 144Hz', 'MN-003', 8, 5990.00)
ON CONFLICT (sku) DO NOTHING;
