using JasperFx;

namespace Chat.Storage;

public static class WriteConflict
{
    // Marten throws one AggregateException when several documents in a SaveChangesAsync batch collide.
    public static bool IsWriteConflict(this Exception exception) => exception switch
    {
        ConcurrencyException or DocumentAlreadyExistsException => true,
        AggregateException aggregate => aggregate.InnerExceptions.All(IsWriteConflict),
        _ => false
    };
}
