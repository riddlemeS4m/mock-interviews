namespace MockInterviews.Models.ViewModels.InterviewEventsController
{
    public class InterviewerOptionViewModelComparer : IEqualityComparer<InterviewerOptionViewModel>
    {
        public bool Equals(InterviewerOptionViewModel? x, InterviewerOptionViewModel? y)
        {
            if (x is null || y is null)
                return false;

            return x.Name == y.Name &&
                   x.Id == y.Id &&
                   x.Technical == y.Technical &&
                   x.Behavioral == y.Behavioral;
        }

        public int GetHashCode(InterviewerOptionViewModel obj)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + obj.Name.GetHashCode();
                hash = hash * 23 + obj.Id.GetHashCode();
                hash = hash * 23 + obj.Technical.GetHashCode();
                hash = hash * 23 + obj.Behavioral.GetHashCode();
                return hash;
            }
        }
    }
}
