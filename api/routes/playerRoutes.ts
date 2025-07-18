
import { Router } from 'express';
import { playerController } from '../controllers/playerController';
import { authenticateToken } from '../middleware/auth';
import { rateLimiter } from '../middleware/rateLimiter';

const router = Router();

// Public routes
router.post('/register', rateLimiter, playerController.register);
router.post('/login', rateLimiter, playerController.login);

// Protected routes
router.post('/logout', authenticateToken, playerController.logout);
router.get('/profile', authenticateToken, playerController.getProfile);
router.put('/profile', authenticateToken, rateLimiter, playerController.updateProfile);

export default router;
