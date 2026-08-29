import { Heart } from 'lucide-react';

export default function Footer() {
  return (
    <footer className="border-t border-border bg-card py-3 sm:py-4">
      <div className="container mx-auto px-4 text-center text-xs text-muted-foreground sm:text-sm">
        © 2025. Built with{' '}
        <Heart className="inline h-3 w-3 fill-destructive text-destructive" />{' '}
        using{' '}
        <a
          href="https://caffeine.ai"
          target="_blank"
          rel="noopener noreferrer"
          className="font-medium text-foreground transition-colors hover:text-primary"
        >
          caffeine.ai
        </a>
      </div>
    </footer>
  );
}
