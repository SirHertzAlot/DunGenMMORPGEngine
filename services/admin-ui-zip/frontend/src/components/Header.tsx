import { useState } from 'react';
import { Grid3x3, LayoutDashboard, Menu, FolderOpen, LogIn, LogOut, User, Swords, Eye, FileCode, Database } from 'lucide-react';
import { useNavigate, useRouterState } from '@tanstack/react-router';
import { useInternetIdentity } from '../hooks/useInternetIdentity';
import { useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetTrigger } from '@/components/ui/sheet';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';

export default function Header() {
  const navigate = useNavigate();
  const routerState = useRouterState();
  const queryClient = useQueryClient();
  const [isOpen, setIsOpen] = useState(false);
  const { login, clear, loginStatus, identity, isInitializing } = useInternetIdentity();
  
  // Safe access to router state with null check
  const currentPath = routerState?.location?.pathname ?? '/';

  const isAuthenticated = !!identity;
  const isLoggingIn = loginStatus === 'logging-in' || isInitializing;

  const handleAdminClick = () => {
    navigate({ to: '/admin' });
    setIsOpen(false);
  };

  const handleGeneratorClick = () => {
    navigate({ to: '/generator' });
    setIsOpen(false);
  };

  const handleFileManagerClick = () => {
    navigate({ to: '/file-manager' });
    setIsOpen(false);
  };

  const handleGameObjectGeneratorClick = () => {
    navigate({ to: '/game-object-generator' });
    setIsOpen(false);
  };

  const handleVisualizerClick = () => {
    navigate({ to: '/visualizer' });
    setIsOpen(false);
  };

  const handleYAMLServiceClick = () => {
    navigate({ to: '/yaml-service' });
    setIsOpen(false);
  };

  const handleGlobalTablesClick = () => {
    navigate({ to: '/global-tables' });
    setIsOpen(false);
  };

  const handleLoginClick = () => {
    navigate({ to: '/login' });
    setIsOpen(false);
  };

  const handleAuth = async () => {
    if (isAuthenticated) {
      await clear();
      queryClient.clear();
      navigate({ to: '/login' });
    } else {
      try {
        await login();
      } catch (error: any) {
        console.error('Login error:', error);
        if (error.message === 'User is already authenticated') {
          await clear();
          setTimeout(() => login(), 300);
        }
      }
    }
    setIsOpen(false);
  };

  const getPrincipalShort = (): string => {
    if (!identity) return '';
    const principal = identity.getPrincipal().toString();
    return `${principal.slice(0, 5)}...${principal.slice(-3)}`;
  };

  return (
    <header className="border-b border-border bg-card">
      <div className="container mx-auto flex h-14 items-center justify-between px-4 sm:h-16 sm:px-6">
        {/* Logo - Responsive sizing */}
        <div className="flex items-center gap-2 sm:gap-3">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-gradient-to-br from-primary to-accent sm:h-10 sm:w-10">
            <Grid3x3 className="h-5 w-5 text-primary-foreground sm:h-6 sm:w-6" />
          </div>
          <div className="hidden sm:block">
            <h1 className="text-lg font-bold tracking-tight sm:text-xl">3D Terrain Grid Builder</h1>
            <p className="text-xs text-muted-foreground">Powered by Perlin Noise & Three.js</p>
          </div>
          <div className="block sm:hidden">
            <h1 className="text-base font-bold tracking-tight">DunGen</h1>
          </div>
        </div>

        {/* Desktop Navigation */}
        <nav className="hidden items-center gap-2 lg:flex">
          <Button
            variant={currentPath === '/admin' || currentPath === '/' ? 'default' : 'ghost'}
            size="sm"
            onClick={handleAdminClick}
          >
            <LayoutDashboard className="mr-2 h-4 w-4" />
            Dashboard
          </Button>
          <Button
            variant={currentPath === '/generator' ? 'default' : 'ghost'}
            size="sm"
            onClick={handleGeneratorClick}
          >
            <Grid3x3 className="mr-2 h-4 w-4" />
            Terrain
          </Button>
          <Button
            variant={currentPath === '/file-manager' ? 'default' : 'ghost'}
            size="sm"
            onClick={handleFileManagerClick}
          >
            <FolderOpen className="mr-2 h-4 w-4" />
            Files
          </Button>
          <Button
            variant={currentPath === '/game-object-generator' ? 'default' : 'ghost'}
            size="sm"
            onClick={handleGameObjectGeneratorClick}
          >
            <Swords className="mr-2 h-4 w-4" />
            Objects
          </Button>
          <Button
            variant={currentPath === '/visualizer' ? 'default' : 'ghost'}
            size="sm"
            onClick={handleVisualizerClick}
          >
            <Eye className="mr-2 h-4 w-4" />
            Visualizer
          </Button>
          <Button
            variant={currentPath === '/yaml-service' ? 'default' : 'ghost'}
            size="sm"
            onClick={handleYAMLServiceClick}
          >
            <FileCode className="mr-2 h-4 w-4" />
            YAML
          </Button>
          <Button
            variant={currentPath === '/global-tables' ? 'default' : 'ghost'}
            size="sm"
            onClick={handleGlobalTablesClick}
          >
            <Database className="mr-2 h-4 w-4" />
            Tables
          </Button>

          {/* Auth Button - Desktop */}
          {isAuthenticated ? (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="outline" size="sm" className="gap-2">
                  <User className="h-4 w-4" />
                  <span className="hidden lg:inline">{getPrincipalShort()}</span>
                  <Badge variant="secondary" className="hidden xl:inline-flex">
                    Authenticated
                  </Badge>
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-56">
                <DropdownMenuLabel>Account</DropdownMenuLabel>
                <DropdownMenuSeparator />
                <div className="px-2 py-1.5 text-xs text-muted-foreground">
                  <div className="font-medium">Principal ID:</div>
                  <div className="mt-1 break-all font-mono text-[10px]">
                    {identity?.getPrincipal().toString()}
                  </div>
                </div>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={handleAuth} className="text-destructive">
                  <LogOut className="mr-2 h-4 w-4" />
                  Sign Out
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          ) : (
            <Button
              variant="default"
              size="sm"
              onClick={handleAuth}
              disabled={isLoggingIn}
            >
              {isLoggingIn ? (
                <>
                  <div className="mr-2 h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent" />
                  Connecting...
                </>
              ) : (
                <>
                  <LogIn className="mr-2 h-4 w-4" />
                  Sign In
                </>
              )}
            </Button>
          )}
        </nav>

        {/* Mobile Navigation */}
        <Sheet open={isOpen} onOpenChange={setIsOpen}>
          <SheetTrigger asChild className="lg:hidden">
            <Button variant="ghost" size="icon">
              <Menu className="h-5 w-5" />
              <span className="sr-only">Toggle menu</span>
            </Button>
          </SheetTrigger>
          <SheetContent side="right" className="w-64">
            <ScrollArea className="h-full">
              <nav className="flex flex-col gap-4 pt-8">
                {/* Auth Status - Mobile */}
                {isAuthenticated && (
                  <div className="mb-2 rounded-lg border border-border bg-muted/50 p-3">
                    <div className="flex items-center gap-2 mb-2">
                      <User className="h-4 w-4 text-primary" />
                      <Badge variant="secondary" className="text-xs">
                        Authenticated
                      </Badge>
                    </div>
                    <div className="text-[10px] text-muted-foreground break-all font-mono">
                      {identity?.getPrincipal().toString()}
                    </div>
                  </div>
                )}

                <Button
                  variant={currentPath === '/admin' || currentPath === '/' ? 'default' : 'ghost'}
                  size="lg"
                  onClick={handleAdminClick}
                  className="w-full justify-start"
                >
                  <LayoutDashboard className="mr-3 h-5 w-5" />
                  Admin Dashboard
                </Button>
                <Button
                  variant={currentPath === '/generator' ? 'default' : 'ghost'}
                  size="lg"
                  onClick={handleGeneratorClick}
                  className="w-full justify-start"
                >
                  <Grid3x3 className="mr-3 h-5 w-5" />
                  Terrain Generator
                </Button>
                <Button
                  variant={currentPath === '/file-manager' ? 'default' : 'ghost'}
                  size="lg"
                  onClick={handleFileManagerClick}
                  className="w-full justify-start"
                >
                  <FolderOpen className="mr-3 h-5 w-5" />
                  File Manager
                </Button>
                <Button
                  variant={currentPath === '/game-object-generator' ? 'default' : 'ghost'}
                  size="lg"
                  onClick={handleGameObjectGeneratorClick}
                  className="w-full justify-start"
                >
                  <Swords className="mr-3 h-5 w-5" />
                  Game Objects
                </Button>
                <Button
                  variant={currentPath === '/visualizer' ? 'default' : 'ghost'}
                  size="lg"
                  onClick={handleVisualizerClick}
                  className="w-full justify-start"
                >
                  <Eye className="mr-3 h-5 w-5" />
                  Visualizer
                </Button>
                <Button
                  variant={currentPath === '/yaml-service' ? 'default' : 'ghost'}
                  size="lg"
                  onClick={handleYAMLServiceClick}
                  className="w-full justify-start"
                >
                  <FileCode className="mr-3 h-5 w-5" />
                  YAML Service
                </Button>
                <Button
                  variant={currentPath === '/global-tables' ? 'default' : 'ghost'}
                  size="lg"
                  onClick={handleGlobalTablesClick}
                  className="w-full justify-start"
                >
                  <Database className="mr-3 h-5 w-5" />
                  Global Tables
                </Button>

                <div className="my-2 border-t border-border" />

                {/* Auth Button - Mobile */}
                <Button
                  variant={isAuthenticated ? 'destructive' : 'default'}
                  size="lg"
                  onClick={handleAuth}
                  disabled={isLoggingIn}
                  className="w-full justify-start"
                >
                  {isLoggingIn ? (
                    <>
                      <div className="mr-3 h-5 w-5 animate-spin rounded-full border-2 border-current border-t-transparent" />
                      Connecting...
                    </>
                  ) : isAuthenticated ? (
                    <>
                      <LogOut className="mr-3 h-5 w-5" />
                      Sign Out
                    </>
                  ) : (
                    <>
                      <LogIn className="mr-3 h-5 w-5" />
                      Sign In
                    </>
                  )}
                </Button>
              </nav>
            </ScrollArea>
          </SheetContent>
        </Sheet>
      </div>
    </header>
  );
}
