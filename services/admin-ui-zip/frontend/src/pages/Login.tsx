import { useEffect } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { useInternetIdentity } from '../hooks/useInternetIdentity';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Grid3x3, Loader2, LogIn } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';

export default function Login() {
  const navigate = useNavigate();
  const { login, loginStatus, identity, isInitializing } = useInternetIdentity();

  // Redirect to admin dashboard if already logged in
  useEffect(() => {
    if (identity && loginStatus === 'success') {
      navigate({ to: '/admin' });
    }
  }, [identity, loginStatus, navigate]);

  const handleLogin = async () => {
    try {
      await login();
    } catch (error: any) {
      console.error('Login error:', error);
    }
  };

  const isLoggingIn = loginStatus === 'logging-in' || isInitializing;
  const isError = loginStatus === 'loginError';

  return (
    <div className="flex h-full w-full items-center justify-center overflow-y-auto p-4">
      <Card className="w-full max-w-md">
        <CardHeader className="space-y-4 text-center">
          <div className="flex justify-center">
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl border-4 border-primary/30 bg-gradient-to-br from-primary to-accent shadow-2xl">
              <Grid3x3 className="h-8 w-8 text-primary-foreground" />
            </div>
          </div>
          <div>
            <CardTitle className="text-2xl">Welcome to DunGen</CardTitle>
            <CardDescription className="mt-2">
              Sign in with Internet Identity to access the 3D Terrain Grid Builder
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {isError && (
            <Alert variant="destructive">
              <AlertDescription>
                Failed to authenticate. Please try again.
              </AlertDescription>
            </Alert>
          )}
          
          <Button
            onClick={handleLogin}
            disabled={isLoggingIn}
            className="w-full"
            size="lg"
          >
            {isLoggingIn ? (
              <>
                <Loader2 className="mr-2 h-5 w-5 animate-spin" />
                Connecting...
              </>
            ) : (
              <>
                <LogIn className="mr-2 h-5 w-5" />
                Sign In with Internet Identity
              </>
            )}
          </Button>

          <div className="rounded-lg border border-border bg-muted/50 p-4">
            <h3 className="mb-2 text-sm font-semibold">What is Internet Identity?</h3>
            <p className="text-xs text-muted-foreground">
              Internet Identity is a secure, anonymous authentication system built on the Internet Computer. 
              Your identity is cryptographically secured and never shared with third parties.
            </p>
          </div>

          <div className="space-y-2 text-xs text-muted-foreground">
            <p className="font-medium">After signing in, you'll have access to:</p>
            <ul className="ml-4 space-y-1 list-disc">
              <li>3D Terrain Generator with advanced erosion simulation</li>
              <li>File Manager for uploading and managing 3D assets</li>
              <li>Grid configuration save and export functionality</li>
              <li>ZIP file extraction and batch processing</li>
            </ul>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
