import { Outlet } from 'react-router-dom';
import { TopBar } from '../components/layout/TopBar';
import { Footer } from '../components/layout/Footer';

export function PortalLayout() {
  return (
    <div className="flex flex-col min-h-screen">
      <TopBar />
      <main className="flex-1">
        <Outlet />
      </main>
      <Footer />
    </div>
  );
}
