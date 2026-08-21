import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Tibbi Nav HRM",
  description: "HR-платформа Tibbi Nav",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="ru">
      <body>{children}</body>
    </html>
  );
}
