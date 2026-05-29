import { Link } from 'react-router-dom';
import { GraduationCap } from 'lucide-react';

const footerLinks = [
  {
    title: '关于我们',
    items: ['平台介绍', '联系我们', '隐私政策', '服务条款'],
  },
  {
    title: '帮助中心',
    items: ['使用指南', '常见问题', '视频教程', '意见反馈'],
  },
  {
    title: '友情链接',
    items: ['教育部', '国家职业教育智慧教育平台', '高等教育出版社'],
  },
];

const qrCodes = [
  { label: '微信公众号', src: '', color: '#07C160' },
  { label: '微信服务号', src: '', color: '#0056D2' },
  { label: '微信视频号', src: '', color: '#FA9D3B' },
];

export function Footer() {
  return (
    <footer className="bg-[#1a1a2e] text-white">
      {/* Top section */}
      <div className="mx-auto max-w-[1400px] px-4 sm:px-6 lg:px-8 py-12">
        <div className="grid gap-8 sm:grid-cols-2 lg:grid-cols-5">
          {/* Brand */}
          <div className="lg:col-span-2">
            <div className="flex items-center gap-2.5 mb-4">
              <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-white/15">
                <GraduationCap className="h-5 w-5 text-white" />
              </span>
              <span className="text-lg font-bold">易课通资源库</span>
            </div>
            <p className="text-sm text-white/60 leading-relaxed max-w-xs">
              面向职业教育的数字化教学资源平台，汇聚精品课程、教学素材、知识图谱，
              为师生提供一站式的教学与学习体验。
            </p>
          </div>

          {/* Links */}
          {footerLinks.map(group => (
            <div key={group.title}>
              <h4 className="text-sm font-semibold mb-3">{group.title}</h4>
              <ul className="space-y-2">
                {group.items.map(item => (
                  <li key={item}>
                    <Link to="#" className="text-sm text-white/50 hover:text-white/80 transition-colors">
                      {item}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </div>

      {/* QR Codes */}
      <div className="border-t border-white/10">
        <div className="mx-auto max-w-[1400px] px-4 sm:px-6 lg:px-8 py-8">
          <div className="flex flex-wrap items-center justify-center gap-8">
            {qrCodes.map(qr => (
              <div key={qr.label} className="text-center">
                <div
                  className="mx-auto mb-2 h-20 w-20 rounded-lg flex items-center justify-center text-xs text-white/40"
                  style={{ background: `${qr.color}22`, border: `1px solid ${qr.color}33` }}
                >
                  QR
                </div>
                <p className="text-xs text-white/50">{qr.label}</p>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Bottom bar */}
      <div className="border-t border-white/10">
        <div className="mx-auto max-w-[1400px] px-4 sm:px-6 lg:px-8 py-4 flex flex-col sm:flex-row items-center justify-between gap-2">
          <p className="text-xs text-white/40">
            Copyright © 2024-2026 易课通资源库 &nbsp;|&nbsp;
            <Link to="#" className="hover:text-white/60">京ICP备XXXXXXXX号-1</Link>
          </p>
          <div className="flex items-center gap-4 text-xs text-white/40">
            <Link to="#" className="hover:text-white/60">隐私政策</Link>
            <Link to="#" className="hover:text-white/60">服务条款</Link>
            <Link to="#" className="hover:text-white/60">免责声明</Link>
          </div>
        </div>
      </div>
    </footer>
  );
}
