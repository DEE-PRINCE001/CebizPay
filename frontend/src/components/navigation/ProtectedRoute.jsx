import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { ROUTES } from '../../constants/routes';
import Skeleton from '../common/Skeleton';

/**
 * Route Guard Component.
 * Ensures user is authenticated and possesses required role (if specified) before rendering child routes.
 */
export default function ProtectedRoute({ children, allowedRoles = [] }) {
  const { isAuthenticated, user, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return (
      <div className="min-h-screen bg-canvas-bg flex items-center justify-center p-6">
        <div className="max-w-md w-full bg-white p-8 rounded-2xl shadow-xs border border-slate-100 space-y-4">
          <Skeleton variant="circle" />
          <Skeleton variant="text" count={3} />
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    // Redirect to login preserving intended destination
    return <Navigate to={ROUTES.LOGIN} state={{ from: location }} replace />;
  }

  // Optional Role Check
  if (allowedRoles.length > 0 && user?.role && !allowedRoles.includes(user.role)) {
    return <Navigate to={ROUTES.DASHBOARD} replace />;
  }

  return children;
}
