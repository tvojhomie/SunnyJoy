using System;

namespace AlienProject.WebWrapper
{

    public interface IPushRegistrationChangeSource
    {
        event Action<PushRegistration> RegistrationChanged;
    }
}
