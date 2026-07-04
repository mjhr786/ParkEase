
import { useState } from 'react';
import toast from 'react-hot-toast';

export default function EmployeeForm({ onSubmit, onCancel }) {
  const [formData, setFormData] = useState({
    email: '',
    priority: 'Normal'
  });
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!formData.email) {
      toast.error('Email is required');
      return;
    }

    setLoading(true);
    try {
      await onSubmit(formData);
      setFormData({ email: '', priority: 'Normal' });
      toast.success('Employee added successfully');
    } catch (error) {
      toast.error(error.message || 'Failed to add employee');
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="employee-form">
      <div className="form-group">
        <label htmlFor="email">Employee Email</label>
        <input
          type="email"
          id="email"
          value={formData.email}
          onChange={(e) => setFormData({ ...formData, email: e.target.value })}
          placeholder="employee@company.com"
          required
          disabled={loading}
        />
      </div>

      <div className="form-group">
        <label htmlFor="priority">Priority Level</label>
        <select
          id="priority"
          value={formData.priority}
          onChange={(e) => setFormData({ ...formData, priority: e.target.value })}
          disabled={loading}
        >
          <option value="High">High</option>
          <option value="Normal">Normal</option>
          <option value="Low">Low</option>
        </select>
      </div>

      <div className="form-actions">
        <button
          type="button"
          onClick={onCancel}
          className="btn btn-secondary"
          disabled={loading}
        >
          Cancel
        </button>
        <button
          type="submit"
          className="btn btn-primary"
          disabled={loading}
        >
          {loading ? 'Adding...' : 'Add Employee'}
        </button>
      </div>
    </form>
  );
}
