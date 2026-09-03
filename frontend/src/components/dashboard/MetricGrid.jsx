import React from 'react';
import StatCard from '../common/StatCard';
import { Building2, Users, Clock, UserCheck, ShieldAlert, PiggyBank } from 'lucide-react';

/**
 * Metric stat grid for dashboard overview.
 */
export default function MetricGrid({
  metrics = {},
  loading = false,
  className = ''
}) {
  const {
    organisationsCount = 0,
    individualsCount = 0,
    pendingUsersCount = 0,
    activeUsersCount = 0,
    rejectedUsersCount = 0,
    savingPlansCount = 0
  } = metrics;

  return (
    <div className={`grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3 sm:gap-4 ${className}`}>
      <StatCard
        icon={Building2}
        label="Organisations"
        value={loading ? '...' : organisationsCount.toLocaleString()}
      />
      <StatCard
        icon={Users}
        label="Individuals"
        value={loading ? '...' : individualsCount.toLocaleString()}
        trend="+12%"
        trendType="positive"
      />
      <StatCard
        icon={Clock}
        label="Pending Users"
        value={loading ? '...' : pendingUsersCount.toLocaleString()}
      />
      <StatCard
        icon={UserCheck}
        label="Active Users"
        value={loading ? '...' : activeUsersCount.toLocaleString()}
      />
      <StatCard
        icon={ShieldAlert}
        label="Rejected Users"
        value={loading ? '...' : rejectedUsersCount.toLocaleString()}
        trend="-2%"
        trendType="negative"
      />
      <StatCard
        icon={PiggyBank}
        label="Saving Plans"
        value={loading ? '...' : savingPlansCount.toLocaleString()}
      />
    </div>
  );
}
