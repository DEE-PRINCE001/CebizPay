import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import {
  Briefcase,
  Plus,
  Users,
  Eye,
  CheckCircle,
  XCircle,
  FileText,
  ExternalLink,
  MapPin,
  Clock
} from 'lucide-react';

export default function OrgRecruitment() {
  const [activeTab, setActiveTab] = useState('jobs'); // 'jobs' | 'applications'
  const [showCreateJobModal, setShowCreateJobModal] = useState(false);
  const [selectedApp, setSelectedApp] = useState(null);
  const [showAppModal, setShowAppModal] = useState(false);
  const { showSuccess, showError } = useToast();

  // Job creation state
  const [jobTitle, setJobTitle] = useState('');
  const [jobDept, setJobDept] = useState('Engineering');
  const [jobLocation, setJobLocation] = useState('Lagos, Nigeria (Hybrid)');
  const [jobType, setJobType] = useState('FULL_TIME');
  const [minSalary, setMinSalary] = useState('1200000');
  const [maxSalary, setMaxSalary] = useState('1800000');
  const [jobDesc, setJobDesc] = useState('');

  const [isLoading, setIsLoading] = useState(false);

  // Jobs list
  const [jobs, setJobs] = useState([]);

  // Applications list
  const [applications, setApplications] = useState([]);

  const handleCreateJob = (e) => {
    e.preventDefault();
    const newJob = {
      id: `job-${Date.now()}`,
      title: jobTitle,
      department: jobDept,
      location: jobLocation,
      employmentType: jobType,
      minSalary: parseFloat(minSalary),
      maxSalary: parseFloat(maxSalary),
      currency: 'NGN',
      status: 'PUBLISHED',
      applicationsCount: 0,
      postedAt: new Date().toISOString()
    };
    setJobs((prev) => [newJob, ...prev]);
    showSuccess('Job Published', `"${jobTitle}" is now live on the public careers board.`);
    setShowCreateJobModal(false);
    setJobTitle('');
  };

  const handleUpdateAppStatus = (newStatus) => {
    if (!selectedApp) return;
    setApplications((prev) =>
      prev.map((a) => (a.id === selectedApp.id ? { ...a, status: newStatus } : a))
    );
    showSuccess(
      'Application Updated',
      `${selectedApp.candidateName} moved to status: ${newStatus}.`
    );
    setShowAppModal(false);
  };

  const jobColumns = [
    {
      header: 'Job Opening Title',
      accessor: 'title',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.title}</span>
          <span className="text-[11px] text-slate-400 font-mono">{row.department} • {row.location}</span>
        </div>
      )
    },
    {
      header: 'Compensation Band',
      accessor: 'minSalary',
      render: (row) => (
        <span className="font-mono text-slate-700 font-bold text-xs">
          {formatCurrency(row.minSalary)} – {formatCurrency(row.maxSalary)}
        </span>
      )
    },
    {
      header: 'Applicants',
      accessor: 'applicationsCount',
      render: (row) => <span className="font-bold text-blue-700">{row.applicationsCount} Candidates</span>
    },
    {
      header: 'Status',
      accessor: 'status',
      render: (row) => <Badge status={row.status === 'PUBLISHED' ? 'ACTIVE' : 'DRAFT'} label={row.status} size="sm" />
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <div className="flex items-center justify-end gap-1.5">
          <button
            onClick={() => {
              setActiveTab('applications');
            }}
            className="px-3 py-1.5 bg-blue-50 text-blue-700 hover:bg-blue-100 rounded-lg text-xs font-bold transition-colors"
          >
            View Applicants
          </button>
        </div>
      )
    }
  ];

  const appColumns = [
    {
      header: 'Candidate Name',
      accessor: 'candidateName',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.candidateName}</span>
          <span className="text-[11px] text-slate-400">{row.candidateEmail} • {row.candidatePhone}</span>
        </div>
      )
    },
    {
      header: 'Target Job Position',
      accessor: 'jobTitle',
      render: (row) => <span className="font-semibold text-slate-700 text-xs">{row.jobTitle}</span>
    },
    {
      header: 'Status',
      accessor: 'status',
      render: (row) => <Badge status={row.status} size="sm" />
    },
    {
      header: 'Applied Date',
      accessor: 'appliedAt',
      render: (row) => formatDate(row.appliedAt, true)
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <button
          onClick={() => {
            setSelectedApp(row);
            setShowAppModal(true);
          }}
          className="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-800 rounded-lg text-xs font-bold transition-colors"
        >
          Review Application
        </button>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="Recruitment &amp; Talent Acquisition"
        subtitle="Publish job postings to the public board, review candidate CVs, manage candidate pipelines, and extend offers."
        actions={
          <button
            onClick={() => setShowCreateJobModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <Plus className="w-3.5 h-3.5" />
            Post New Job Opening
          </button>
        }
      />

      <Tabs
        tabs={[
          { id: 'jobs', label: 'Job Postings (Vacancies)', count: jobs.length, icon: Briefcase },
          { id: 'applications', label: 'Candidate Pipeline', count: applications.length, icon: Users }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      {activeTab === 'jobs' && <DataTable columns={jobColumns} data={jobs} />}
      {activeTab === 'applications' && <DataTable columns={appColumns} data={applications} />}

      {/* Create Job Modal */}
      <Modal
        isOpen={showCreateJobModal}
        onClose={() => setShowCreateJobModal(false)}
        title="Post New Job Opening"
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowCreateJobModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleCreateJob} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Publish Job</button>
          </div>
        }
      >
        <form onSubmit={handleCreateJob} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1">Job Title</label>
            <input type="text" required value={jobTitle} onChange={(e) => setJobTitle(e.target.value)} placeholder="e.g. Senior Frontend Engineer" className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-bold" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Department</label>
              <select value={jobDept} onChange={(e) => setJobDept(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl">
                <option value="Engineering">Engineering</option>
                <option value="Product & Design">Product &amp; Design</option>
                <option value="Finance & Accounting">Finance &amp; Accounting</option>
                <option value="Human Resources">Human Resources</option>
              </select>
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Location</label>
              <input type="text" value={jobLocation} onChange={(e) => setJobLocation(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl" />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Min Salary (₦)</label>
              <input type="number" required value={minSalary} onChange={(e) => setMinSalary(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
            </div>
            <div>
              <label className="block font-semibold text-slate-700 mb-1">Max Salary (₦)</label>
              <input type="number" required value={maxSalary} onChange={(e) => setMaxSalary(e.target.value)} className="w-full px-3.5 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold" />
            </div>
          </div>
        </form>
      </Modal>

      {/* Candidate Review Modal */}
      {selectedApp && (
        <Modal
          isOpen={showAppModal}
          onClose={() => setShowAppModal(false)}
          title={`Candidate: ${selectedApp.candidateName}`}
          subtitle={`Applied for ${selectedApp.jobTitle}`}
          footer={
            <div className="flex items-center justify-between w-full">
              <div className="flex gap-2">
                <button
                  onClick={() => handleUpdateAppStatus('ACCEPTED')}
                  className="px-4 py-2 text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-700 rounded-xl"
                >
                  Extend Job Offer
                </button>
                <button
                  onClick={() => handleUpdateAppStatus('SHORTLISTED')}
                  className="px-4 py-2 text-xs font-bold text-blue-700 bg-blue-50 hover:bg-blue-100 rounded-xl"
                >
                  Shortlist
                </button>
                <button
                  onClick={() => handleUpdateAppStatus('REJECTED')}
                  className="px-4 py-2 text-xs font-bold text-rose-700 bg-rose-50 hover:bg-rose-100 rounded-xl"
                >
                  Reject
                </button>
              </div>
              <button
                onClick={() => setShowAppModal(false)}
                className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
              >
                Close
              </button>
            </div>
          }
        >
          <div className="space-y-4 text-xs text-left">
            <div className="p-3 bg-slate-50 rounded-xl border border-slate-200 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <FileText className="w-4 h-4 text-blue-600" />
                <span className="font-semibold text-slate-900">Curriculum Vitae (CV / Resume)</span>
              </div>
              <a
                href={selectedApp.resumeUrl}
                target="_blank"
                rel="noreferrer"
                className="text-blue-600 font-bold hover:underline flex items-center gap-1"
              >
                Download Resume <ExternalLink className="w-3 h-3" />
              </a>
            </div>

            <div>
              <span className="font-bold text-slate-800 block mb-1">Candidate Cover Letter &amp; Statement:</span>
              <p className="p-3 bg-slate-50 rounded-xl border border-slate-200 text-slate-700 leading-relaxed font-mono">
                {selectedApp.coverLetter}
              </p>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}
