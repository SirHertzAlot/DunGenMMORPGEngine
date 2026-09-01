import { RouterProvider, createRouter, createRoute, createRootRoute, Outlet } from '@tanstack/react-router';
import { ThemeProvider } from 'next-themes';
import Header from './components/Header';
import Footer from './components/Footer';
import AdminDashboard from './pages/AdminDashboard';
import TerrainGenerator from './pages/TerrainGenerator';
import FileManager from './pages/FileManager';
import Login from './pages/Login';
import GameObjectGenerator from './pages/GameObjectGenerator';
import GenericVisualizer from './pages/GenericVisualizer';
import YAMLBackendService from './pages/YAMLBackendService';
import GlobalTablesManager from './pages/GlobalTablesManager';
import DebugWidget from './components/DebugWidget';
import ConnectionBanner from './components/ConnectionBanner';

// Layout component with Outlet for child routes
function Layout() {
  return (
    <div className="flex min-h-screen flex-col">
      <Header />
      <ConnectionBanner />
      <main className="flex-1 overflow-hidden">
        <Outlet />
      </main>
      <Footer />
    </div>
  );
}

// Root route with layout - uses Outlet instead of children
const rootRoute = createRootRoute({
  component: Layout,
});

// Define routes
const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  component: AdminDashboard,
});

const adminRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/admin',
  component: AdminDashboard,
});

const generatorRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/generator',
  component: TerrainGenerator,
});

const fileManagerRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/file-manager',
  component: FileManager,
});

const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/login',
  component: Login,
});

const gameObjectGeneratorRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/game-object-generator',
  component: GameObjectGenerator,
});

const visualizerRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/visualizer',
  component: GenericVisualizer,
});

const yamlServiceRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/yaml-service',
  component: YAMLBackendService,
});

const globalTablesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/global-tables',
  component: GlobalTablesManager,
});

// Create route tree
const routeTree = rootRoute.addChildren([
  indexRoute,
  adminRoute,
  generatorRoute,
  fileManagerRoute,
  loginRoute,
  gameObjectGeneratorRoute,
  visualizerRoute,
  yamlServiceRoute,
  globalTablesRoute,
]);

// Create router
const router = createRouter({ routeTree });

// Register router type
declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}

export default function App() {
  return (
    <ThemeProvider attribute="class" defaultTheme="dark" enableSystem>
      <RouterProvider router={router} />
      <DebugWidget />
    </ThemeProvider>
  );
}
